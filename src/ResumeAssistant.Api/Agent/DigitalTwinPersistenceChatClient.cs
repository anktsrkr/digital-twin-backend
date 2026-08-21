using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using AGUI.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ResumeAssistant.Api.Services;
using ResumeAssistant.Core.Models;

namespace ResumeAssistant.Api.Agent;

/// <summary>
/// Delegating ChatClient that intercepts chat completions/streaming turns to persist
/// conversation history and recruiter audit data into MongoDB (user_threads and recruiter_conversations)
/// using standard Microsoft Agent Framework ChatMessage primitives.
/// </summary>
public sealed class DigitalTwinPersistenceChatClient : DelegatingChatClient
{
    private readonly MongoDbChatHistoryProvider _historyProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    public DigitalTwinPersistenceChatClient(
        IChatClient innerClient,
        MongoDbChatHistoryProvider historyProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger)
        : base(innerClient)
    {
        _historyProvider = historyProvider ?? throw new ArgumentNullException(nameof(historyProvider));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        var assistantMsg = new ChatMessage(ChatRole.Assistant, response.Text ?? "");
        if (response.Messages is not null)
        {
            foreach (var m in response.Messages)
            {
                if (m.Contents is not null)
                {
                    foreach (var c in m.Contents)
                    {
                        assistantMsg.Contents.Add(c);
                    }
                }
            }
        }
        _ = PersistTurnAsync(messages, assistantMsg, options, cancellationToken);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var responseBuilder = new StringBuilder();
        var responseContents = new List<AIContent>();

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            if (update.Text is not null)
            {
                responseBuilder.Append(update.Text);
            }

            if (update.Contents is not null)
            {
                responseContents.AddRange(update.Contents);
            }

            yield return update;
        }

        var fullResponse = responseBuilder.ToString();

        // In AG-UI, an intermediate tool call stream emits empty text. Defer persistence until completion.
        if (string.IsNullOrWhiteSpace(fullResponse))
        {
            yield break;
        }

        var assistantMessage = new ChatMessage(ChatRole.Assistant, fullResponse);
        foreach (var content in responseContents)
        {
            assistantMessage.Contents.Add(content);
        }

        _ = PersistTurnAsync(messages, assistantMessage, options, CancellationToken.None);
    }

    private async Task PersistTurnAsync(
        IEnumerable<ChatMessage> messages,
        ChatMessage assistantResponse,
        ChatOptions? options,
        CancellationToken ct)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            string? userId = user?.FindFirst("sub")?.Value 
                ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string? userEmail = user?.FindFirst("email")?.Value 
                ?? user?.FindFirst(ClaimTypes.Email)?.Value;

            // 1. Extract threadId from AG-UI RunAgentInput
            string? threadId = null;
            if (options is not null && options.TryGetRunAgentInput(out var aguiInput) && !string.IsNullOrWhiteSpace(aguiInput.ThreadId))
            {
                threadId = aguiInput.ThreadId;
            }

            // 2. Fallback to query params, header, or user identity
            if (string.IsNullOrWhiteSpace(threadId))
            {
                threadId = httpContext?.Request?.Query["threadId"].FirstOrDefault()
                    ?? httpContext?.Request?.Headers["X-Thread-Id"].FirstOrDefault()
                    ?? httpContext?.Request?.Query["session_id"].FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(threadId))
            {
                threadId = !string.IsNullOrWhiteSpace(userEmail) 
                    ? $"thread_{userEmail.Replace("@", "_").Replace(".", "_")}" 
                    : (!string.IsNullOrWhiteSpace(userId) ? $"thread_{userId}" : "thread_default");
            }

            var lastUserMsg = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";

            // Map assistant message to PersistedChatMessage
            var persistedAssistant = MongoDbChatHistoryProvider.MapToPersistedChatMessage(assistantResponse);

            // Merge any preceding tool calls / results sent in incoming messages for this turn
            var msgList = messages.ToList();
            int lastUserIdx = msgList.FindLastIndex(m => m.Role == ChatRole.User);
            var recentToolMessages = (lastUserIdx >= 0 ? msgList.Skip(lastUserIdx) : msgList)
                .Where(m => m.Role != ChatRole.User && m.Role != ChatRole.System);

            foreach (var msg in recentToolMessages)
            {
                var p = MongoDbChatHistoryProvider.MapToPersistedChatMessage(msg);
                foreach (var tc in p.ToolCalls)
                {
                    var existing = persistedAssistant.ToolCalls.FirstOrDefault(x => x.Id == tc.Id);
                    if (existing != null)
                    {
                        if (string.IsNullOrWhiteSpace(existing.Result)) existing.Result = tc.Result;
                        if (string.IsNullOrWhiteSpace(existing.Arguments)) existing.Arguments = tc.Arguments;
                    }
                    else
                    {
                        persistedAssistant.ToolCalls.Add(tc);
                    }
                }
                foreach (var cit in p.Citations)
                {
                    if (!persistedAssistant.Citations.Any(c => c.SourceName == cit.SourceName && c.Title == cit.Title))
                    {
                        persistedAssistant.Citations.Add(cit);
                    }
                }
            }

            // 3. Persist turn directly via MongoDbChatHistoryProvider
            await _historyProvider.PersistTurnAsync(
                threadId: threadId,
                userMsg: lastUserMsg,
                assistantMsg: persistedAssistant,
                userId: userId,
                userEmail: userEmail,
                ct: ct);

            _logger.LogInformation("💾 Persisted conversation turn with {ToolCount} tool(s) & {CitCount} citation(s) to MongoDB user_threads [{ThreadId}] for {User}",
                persistedAssistant.ToolCalls.Count, persistedAssistant.Citations.Count, threadId, userEmail ?? userId ?? "Anonymous");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Could not persist chat turn to MongoDB.");
        }
    }
}
