namespace ResumeAssistant.Api.Services;

public sealed record DailyQuotaResult(
    bool Allowed,
    int CurrentCount,
    int Remaining,
    bool IsNudgeThreshold,
    int MaxDailyLimit = 10);

public interface IDailyQuotaService
{
    Task<DailyQuotaResult> CheckAndIncrementAsync(
        string userId,
        string? email = null,
        bool isExemptAction = false,
        CancellationToken cancellationToken = default);

    Task<DailyQuotaResult> GetCurrentQuotaAsync(
        string userId,
        string? email = null,
        CancellationToken cancellationToken = default);
}
