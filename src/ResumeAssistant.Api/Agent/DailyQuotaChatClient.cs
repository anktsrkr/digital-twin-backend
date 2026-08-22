using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using ResumeAssistant.Api.Services;

namespace ResumeAssistant.Api.Agent;

/// <summary>
/// Delegating ChatClient that enforces the 10 questions/day quota per recruiter email/user ID.
/// Exempts direct 'Download Resume' and 'Book a Call' / calendar actions from consuming quota.
/// When turn 8 or 9 is reached, injects a system directive to steer toward meeting booking.
/// When turn 10+ is reached, terminates with 0 token cost and presents booking/resume CTAs.
/// </summary>
public sealed class DailyQuotaChatClient : DelegatingChatClient
{
    private readonly IDailyQuotaService _dailyQuotaService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DailyQuotaChatClient> _logger;

    private const int MaxUserMessageLength = 1000;

    private static readonly Regex ExemptActionsRegex = new(
        @"(?:download\s+(?:resume|cv|pdf)|(?:get|show|view|book|check|schedule)\s+(?:calendar|slot|appointment|interview|call|availability)|when\s+is\s+ankit\s+available)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public DailyQuotaChatClient(
        IChatClient innerClient,
        IDailyQuotaService dailyQuotaService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DailyQuotaChatClient> logger)
        : base(innerClient)
    {
        _dailyQuotaService = dailyQuotaService ?? throw new ArgumentNullException(nameof(dailyQuotaService));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static bool IsExemptAction(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            return ExemptActionsRegex.IsMatch(text);
        }
        catch
        {
            return false;
        }
    }

    private (string userId, string? email) GetUserIdentity()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        var userId = user?.FindFirst("sub")?.Value 
            ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous_user";

        var email = user?.FindFirst("email")?.Value 
            ?? user?.FindFirst(ClaimTypes.Email)?.Value;

        return (userId, email);
    }

    private IEnumerable<ChatMessage> ClampAndPrepareMessages(
        IEnumerable<ChatMessage> messages,
        DailyQuotaResult quota,
        out bool isExempt)
    {
        var msgList = messages.ToList();
        isExempt = false;

        // Find the latest user message
        var lastUserIdx = msgList.FindLastIndex(m => m.Role == ChatRole.User);
        if (lastUserIdx >= 0)
        {
            var userText = msgList[lastUserIdx].Text ?? "";
            isExempt = IsExemptAction(userText);

            if (userText.Length > MaxUserMessageLength)
            {
                userText = userText[..MaxUserMessageLength] + "... [truncated]";
                msgList[lastUserIdx] = new ChatMessage(ChatRole.User, userText);
            }
        }

        // If turn 8 or 9, inject system directive to steer toward booking
        if (quota.IsNudgeThreshold)
        {
            var systemDirective = $"\n\n<CONVERSION_DIRECTIVE>\n[SYSTEM NOTICE: This is turn {quota.CurrentCount} of {quota.MaxDailyLimit} for today. Provide a sharp, concise answer and conclude with an invitation to book a 15-minute or 30-minute intro call on my calendar below.]\n</CONVERSION_DIRECTIVE>";
            
            var sysIdx = msgList.FindIndex(m => m.Role == ChatRole.System);
            if (sysIdx >= 0)
            {
                var existingSys = msgList[sysIdx].Text ?? "";
                if (!existingSys.Contains("<CONVERSION_DIRECTIVE>"))
                {
                    msgList[sysIdx] = new ChatMessage(ChatRole.System, existingSys + systemDirective);
                }
            }
            else
            {
                msgList.Insert(0, new ChatMessage(ChatRole.System, systemDirective));
            }
        }

        return msgList;
    }

    private void SetQuotaHeaders(DailyQuotaResult quota)
    {
        try
        {
            var response = _httpContextAccessor.HttpContext?.Response;
            if (response != null && !response.HasStarted)
            {
                response.Headers["X-Daily-Quota-Remaining"] = quota.Remaining.ToString();
                response.Headers["X-Daily-Quota-Count"] = quota.CurrentCount.ToString();
                response.Headers["X-Daily-Quota-Limit"] = quota.MaxDailyLimit.ToString();
            }
        }
        catch
        {
            // Ignore header mutation errors if stream already began
        }
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (userId, email) = GetUserIdentity();
        var lastUserMsg = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var isExempt = IsExemptAction(lastUserMsg);

        var quota = await _dailyQuotaService.CheckAndIncrementAsync(userId, email, isExempt, cancellationToken);
        SetQuotaHeaders(quota);

        if (!quota.Allowed && !isExempt)
        {
            _logger.LogInformation("Recruiter {User} exceeded daily quota ({Count}/{Limit}). Returning conversion wrap-up.", userId, quota.CurrentCount, quota.MaxDailyLimit);
            var wrapUpMessage = "You have explored Ankit Sarkar's Digital Twin today (10/10 questions reached for today)! Let's continue the discussion directly — please choose any open slot on my live calendar below or download my full resume PDF.";
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, wrapUpMessage));
        }

        var processed = ClampAndPrepareMessages(messages, quota, out _);
        options ??= new ChatOptions();
        options.MaxOutputTokens = Math.Max(options.MaxOutputTokens ?? 3072, 3072);

        return await base.GetResponseAsync(processed, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (userId, email) = GetUserIdentity();
        var lastUserMsg = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var isExempt = IsExemptAction(lastUserMsg);

        var quota = await _dailyQuotaService.CheckAndIncrementAsync(userId, email, isExempt, cancellationToken);
        SetQuotaHeaders(quota);

        if (!quota.Allowed && !isExempt)
        {
            _logger.LogInformation("Recruiter {User} exceeded daily quota ({Count}/{Limit}). Yielding conversion wrap-up.", userId, quota.CurrentCount, quota.MaxDailyLimit);
            var wrapUpMessage = "You have explored Ankit Sarkar's Digital Twin today (10/10 questions reached for today)! Let's continue the discussion directly — please choose any open slot on my live calendar below or download my full resume PDF.";
            yield return new ChatResponseUpdate(ChatRole.Assistant, wrapUpMessage);
            yield break;
        }

        var processed = ClampAndPrepareMessages(messages, quota, out _);
        options ??= new ChatOptions();
        options.MaxOutputTokens = Math.Max(options.MaxOutputTokens ?? 3072, 3072);

        await foreach (var update in base.GetStreamingResponseAsync(processed, options, cancellationToken))
        {
            yield return update;
        }
    }
}
