using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MongoDB.Driver;
using ResumeAssistant.Core.Models;

namespace ResumeAssistant.Api.Services;

/// <summary>
/// Official Microsoft Agent Framework <see cref="ChatHistoryProvider"/> implementation that loads
/// and persists conversation history directly to MongoDB Atlas collection <c>user_threads</c>.
/// The session key is stored inside <see cref="AgentSession.StateBag"/> so it roundtrips automatically
/// across session serialization.
/// </summary>
public sealed class MongoDbChatHistoryProvider : ChatHistoryProvider
{
    private readonly ProviderSessionState<State> _sessionState;
    private IReadOnlyList<string>? _stateKeys;
    private readonly IMongoCollection<UserThread>? _collection;
    private readonly IMongoCollection<RecruiterProfile>? _profilesCollection;

    public MongoDbChatHistoryProvider(
        IMongoDatabase? database,
        Func<AgentSession?, State>? stateInitializer = null,
        string? stateKey = null)
    {
        if (database is not null)
        {
            _collection = database.GetCollection<UserThread>("user_threads");
            _profilesCollection = database.GetCollection<RecruiterProfile>("recruiter_profiles");

            // Ensure 30-day TTL index on user_threads so inactive chat histories auto-prune
            _ = Task.Run(async () =>
            {
                try
                {
                    var keys = Builders<UserThread>.IndexKeys.Ascending(t => t.LastUpdatedAt);
                    var opts = new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(30), Name = "user_threads_ttl_30d" };
                    await _collection.Indexes.CreateOneAsync(new CreateIndexModel<UserThread>(keys, opts));
                }
                catch
                {
                    // Ignore index creation error on startup
                }
            });
        }

        _sessionState = new ProviderSessionState<State>(
            stateInitializer ?? (_ => new State(Guid.NewGuid().ToString("N"))),
            stateKey ?? GetType().Name);
    }

    public override IReadOnlyList<string> StateKeys => _stateKeys ??= [_sessionState.StateKey];

    public string GetSessionDbKey(AgentSession session)
        => _sessionState.GetOrInitializeState(session).SessionDbKey;

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (_collection is null) return [];

        var state = _sessionState.GetOrInitializeState(context.Session);
        var thread = await _collection.Find(t => t.ThreadId == state.SessionDbKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (thread == null || thread.Messages.Count == 0)
        {
            return [];
        }

        // Sliding window: keep only the last 6 messages (3 user+assistant turns).
        // Telemetry shows each prior turn adds 3-4k tokens of history; without a cap,
        // input tokens grow unboundedly and degrade synthesis quality for dense ADR topics.
        const int MaxHistoryMessages = 6;

        return thread.Messages
            .TakeLast(MaxHistoryMessages)
            .Select(m => new ChatMessage(
                m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant,
                m.Content));
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        if (_collection is null) return;

        var state = _sessionState.GetOrInitializeState(context.Session);
        var allNewMessages = context.RequestMessages.Concat(context.ResponseMessages ?? []);
        var persistedMsgs = allNewMessages
            .Where(m => m.Role != ChatRole.System && (!string.IsNullOrWhiteSpace(m.Text) || m.Contents.Any(c => c is FunctionCallContent or FunctionResultContent)))
            .Select(MapToPersistedChatMessage)
            .ToList();

        if (persistedMsgs.Count == 0) return;

        var update = Builders<UserThread>.Update
            .PushEach(t => t.Messages, persistedMsgs, slice: -40)
            .Set(t => t.LastUpdatedAt, DateTimeOffset.UtcNow)
            .SetOnInsert(t => t.ThreadId, state.SessionDbKey)
            .SetOnInsert(t => t.CreatedAt, DateTimeOffset.UtcNow);

        await _collection.UpdateOneAsync(
            t => t.ThreadId == state.SessionDbKey,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <summary>
    /// Direct persistence method for chat client interception pipeline.
    /// Appends user message and completed streamed assistant response along with tool calls and grounded citations to MongoDB user_threads.
    /// </summary>
    public async Task PersistTurnAsync(
        string threadId,
        string userMsg,
        PersistedChatMessage assistantMsg,
        string? userId = null,
        string? userEmail = null,
        CancellationToken ct = default)
    {
        if (_collection is null || string.IsNullOrWhiteSpace(threadId)) return;

        var newMessages = new List<PersistedChatMessage>();
        if (!string.IsNullOrWhiteSpace(userMsg))
        {
            newMessages.Add(new PersistedChatMessage
            {
                Role = "user",
                Content = userMsg,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        if (assistantMsg != null && (!string.IsNullOrWhiteSpace(assistantMsg.Content) || assistantMsg.ToolCalls.Count > 0))
        {
            newMessages.Add(assistantMsg);
        }

        if (newMessages.Count == 0) return;

        var filter = Builders<UserThread>.Filter.Eq(t => t.ThreadId, threadId);
        var update = Builders<UserThread>.Update
            .PushEach(t => t.Messages, newMessages, slice: -40)
            .Set(t => t.LastUpdatedAt, DateTimeOffset.UtcNow)
            .SetOnInsert(t => t.ThreadId, threadId)
            .SetOnInsert(t => t.UserId, userId)
            .SetOnInsert(t => t.UserEmail, userEmail)
            .SetOnInsert(t => t.Title, userMsg.Length > 40 ? userMsg[..40] + "..." : userMsg)
            .SetOnInsert(t => t.CreatedAt, DateTimeOffset.UtcNow);

        await _collection.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = true },
            ct).ConfigureAwait(false);

        // Upsert recruiter profile metadata for domain analytics
        var recruiterId = userEmail ?? userId;
        if (_profilesCollection is not null && !string.IsNullOrWhiteSpace(recruiterId))
        {
            try
            {
                var profileFilter = Builders<RecruiterProfile>.Filter.Eq(p => p.Id, recruiterId);
                string domain = recruiterId.Contains('@') ? recruiterId.Split('@')[^1] : "recruiter.internal";
                var profileUpdate = Builders<RecruiterProfile>.Update
                    .SetOnInsert(p => p.Id, recruiterId)
                    .SetOnInsert(p => p.Email, recruiterId.Contains('@') ? recruiterId : $"{recruiterId}@recruiter.internal")
                    .SetOnInsert(p => p.Domain, domain)
                    .SetOnInsert(p => p.FirstLoginAt, DateTimeOffset.UtcNow)
                    .Set(p => p.LastActiveAt, DateTimeOffset.UtcNow)
                    .Inc(p => p.TotalMessages, 1);

                await _profilesCollection.UpdateOneAsync(profileFilter, profileUpdate, new UpdateOptions { IsUpsert = true }, ct).ConfigureAwait(false);
            }
            catch
            {
                // Silently ignore profile metric update errors
            }
        }
    }

    /// <summary>
    /// Maps a Microsoft.Extensions.AI <see cref="ChatMessage"/> to a <see cref="PersistedChatMessage"/>.
    /// </summary>
    public static PersistedChatMessage MapToPersistedChatMessage(ChatMessage m)
    {
        var msg = new PersistedChatMessage
        {
            Id = m.MessageId ?? Guid.NewGuid().ToString("N"),
            Role = m.Role.Value,
            Content = m.Text ?? string.Empty,
            Timestamp = DateTimeOffset.UtcNow
        };

        if (m.Contents is not null)
        {
            foreach (var c in m.Contents)
            {
                if (c is FunctionCallContent fcc)
                {
                    msg.ToolCalls.Add(new PersistedToolCall
                    {
                        Id = fcc.CallId ?? Guid.NewGuid().ToString("N"),
                        Name = fcc.Name,
                        Arguments = fcc.Arguments is not null ? JsonSerializer.Serialize(fcc.Arguments) : "{}"
                    });
                }
                else if (c is FunctionResultContent frc)
                {
                    var match = msg.ToolCalls.FirstOrDefault(t => t.Id == frc.CallId);
                    var resultJson = frc.Result is string s ? s : (frc.Result is not null ? JsonSerializer.Serialize(frc.Result) : null);
                    if (match != null)
                    {
                        match.Result = resultJson;
                    }
                    else
                    {
                        msg.ToolCalls.Add(new PersistedToolCall
                        {
                            Id = frc.CallId ?? Guid.NewGuid().ToString("N"),
                            Name = "ToolResult",
                            Result = resultJson
                        });
                    }

                    ExtractCitationsFromObject(frc.Result, msg.Citations);
                }
            }
        }

        return msg;
    }

    private static void ExtractCitationsFromObject(object? result, List<CitationDto> targetList)
    {
        if (result is null) return;

        try
        {
            var jsonStr = result is string s ? s : JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            JsonElement citArray = default;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("citations", out var arrProp) && arrProp.ValueKind == JsonValueKind.Array)
            {
                citArray = arrProp;
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                citArray = root;
            }

            if (citArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in citArray.EnumerateArray())
                {
                    var sourceName = el.TryGetProperty("source_name", out var sn) ? sn.GetString() : (el.TryGetProperty("sourceName", out var sn2) ? sn2.GetString() : null);
                    var title = el.TryGetProperty("title", out var t) ? t.GetString() : null;
                    var sourceLink = el.TryGetProperty("source_link", out var sl) ? sl.GetString() : (el.TryGetProperty("sourceLink", out var sl2) ? sl2.GetString() : null);
                    var category = el.TryGetProperty("category", out var cat) ? cat.GetString() : "Experience";
                    var company = el.TryGetProperty("company", out var comp) ? comp.GetString() : null;
                    var role = el.TryGetProperty("role", out var r) ? r.GetString() : null;
                    var period = el.TryGetProperty("period", out var p) ? p.GetString() : null;
                    var content = el.TryGetProperty("content", out var c) ? c.GetString() : null;

                    string[]? technologies = null;
                    if (el.TryGetProperty("technologies", out var techProp) && techProp.ValueKind == JsonValueKind.Array)
                    {
                        technologies = techProp.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    }

                    double? similarity = el.TryGetProperty("similarity", out var simProp) && simProp.TryGetDouble(out var d) ? d : null;

                    if (!string.IsNullOrWhiteSpace(sourceName) || !string.IsNullOrWhiteSpace(title))
                    {
                        var citation = new CitationDto
                        {
                            SourceName = sourceName ?? title ?? "Resume",
                            Title = title ?? sourceName,
                            SourceLink = sourceLink,
                            Category = category,
                            Company = company,
                            Role = role,
                            Period = period,
                            Content = content,
                            Technologies = technologies,
                            Similarity = similarity
                        };

                        if (!targetList.Any(existing => existing.SourceName == citation.SourceName && existing.Title == citation.Title))
                        {
                            targetList.Add(citation);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore unparseable non-json results
        }
    }

    /// <summary>
    /// Represents the per-session state stored in the <see cref="AgentSession.StateBag"/>.
    /// </summary>
    public sealed class State
    {
        public State(string sessionDbKey)
        {
            SessionDbKey = sessionDbKey ?? throw new ArgumentNullException(nameof(sessionDbKey));
        }

        public string SessionDbKey { get; }
    }
}
