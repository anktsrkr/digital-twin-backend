using System.Collections.Concurrent;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace ResumeAssistant.Api.Services;

public sealed class DailyUsageDoc
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("date")]
    public string Date { get; set; } = string.Empty;

    [BsonElement("count")]
    public int Count { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("lastUpdatedAt")]
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class MongoDbDailyQuotaService : IDailyQuotaService
{
    private const int MaxDailyLimit = 10;
    private const int NudgeStartTurn = 8;
    private readonly IMongoCollection<DailyUsageDoc>? _collection;
    private readonly ILogger<MongoDbDailyQuotaService> _logger;
    private readonly ConcurrentDictionary<string, int> _inMemoryFallback = new();
    private static bool _indexEnsured;
    private static readonly SemaphoreSlim _indexLock = new(1, 1);

    public MongoDbDailyQuotaService(
        IMongoDatabase? database,
        ILogger<MongoDbDailyQuotaService> logger)
    {
        _logger = logger;
        if (database != null)
        {
            _collection = database.GetCollection<DailyUsageDoc>("daily_recruiter_usage");
        }
    }

    private async Task EnsureTtlIndexAsync(CancellationToken ct)
    {
        if (_indexEnsured || _collection == null) return;

        await _indexLock.WaitAsync(ct);
        try
        {
            if (_indexEnsured) return;

            var indexKeys = Builders<DailyUsageDoc>.IndexKeys.Ascending(d => d.CreatedAt);
            var indexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(48), Name = "daily_usage_ttl_48h" };
            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<DailyUsageDoc>(indexKeys, indexOptions), cancellationToken: ct);
            _indexEnsured = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure 48h TTL index on daily_recruiter_usage collection");
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static string GetKey(string userId, string? email)
    {
        var primaryId = !string.IsNullOrWhiteSpace(userId) ? userId : (email ?? "anonymous");
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return $"{primaryId}_{today}";
    }

    public async Task<DailyQuotaResult> CheckAndIncrementAsync(
        string userId,
        string? email = null,
        bool isExemptAction = false,
        CancellationToken cancellationToken = default)
    {
        var docKey = GetKey(userId, email);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        if (isExemptAction)
        {
            return await GetCurrentQuotaAsync(userId, email, cancellationToken);
        }

        if (_collection != null)
        {
            await EnsureTtlIndexAsync(cancellationToken);

            var filter = Builders<DailyUsageDoc>.Filter.Eq(d => d.Id, docKey);
            var update = Builders<DailyUsageDoc>.Update
                .Inc(d => d.Count, 1)
                .Set(d => d.LastUpdatedAt, DateTime.UtcNow)
                .SetOnInsert(d => d.Id, docKey)
                .SetOnInsert(d => d.UserId, userId ?? string.Empty)
                .SetOnInsert(d => d.Email, email)
                .SetOnInsert(d => d.Date, today)
                .SetOnInsert(d => d.CreatedAt, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<DailyUsageDoc>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            var updatedDoc = await _collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
            var currentCount = updatedDoc?.Count ?? 1;
            var remaining = Math.Max(0, MaxDailyLimit - currentCount);
            var allowed = currentCount <= MaxDailyLimit;
            var isNudge = currentCount >= NudgeStartTurn && currentCount < MaxDailyLimit;

            _logger.LogInformation("Recruiter {User} turn {Turn}/{Max} (Remaining: {Remaining}, Allowed: {Allowed})",
                userId ?? email, currentCount, MaxDailyLimit, remaining, allowed);

            return new DailyQuotaResult(allowed, currentCount, remaining, isNudge, MaxDailyLimit);
        }

        // In-memory fallback if Mongo is null
        var newCount = _inMemoryFallback.AddOrUpdate(docKey, 1, (_, old) => old + 1);
        var memRemaining = Math.Max(0, MaxDailyLimit - newCount);
        var memAllowed = newCount <= MaxDailyLimit;
        var memNudge = newCount >= NudgeStartTurn && newCount < MaxDailyLimit;

        return new DailyQuotaResult(memAllowed, newCount, memRemaining, memNudge, MaxDailyLimit);
    }

    public async Task<DailyQuotaResult> GetCurrentQuotaAsync(
        string userId,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var docKey = GetKey(userId, email);

        if (_collection != null)
        {
            var filter = Builders<DailyUsageDoc>.Filter.Eq(d => d.Id, docKey);
            var doc = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
            var currentCount = doc?.Count ?? 0;
            var remaining = Math.Max(0, MaxDailyLimit - currentCount);
            var allowed = currentCount < MaxDailyLimit;
            var isNudge = currentCount >= NudgeStartTurn && currentCount < MaxDailyLimit;

            return new DailyQuotaResult(allowed, currentCount, remaining, isNudge, MaxDailyLimit);
        }

        var memCount = _inMemoryFallback.TryGetValue(docKey, out var count) ? count : 0;
        var r = Math.Max(0, MaxDailyLimit - memCount);
        return new DailyQuotaResult(memCount < MaxDailyLimit, memCount, r, memCount >= NudgeStartTurn && memCount < MaxDailyLimit, MaxDailyLimit);
    }
}
