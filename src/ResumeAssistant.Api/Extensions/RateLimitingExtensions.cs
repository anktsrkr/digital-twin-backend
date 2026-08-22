using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ResumeAssistant.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string ChatPolicy = "chat-policy";
    public const string FollowUpPolicy = "followup-policy";
    public const string BookingPolicy = "booking-policy";
    public const string CalendarSlotsPolicy = "calendar-slots-policy";
    public const string AnonPolicy = "anon-policy";
    public const string ConcurrencyPolicy = "concurrency-policy";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/json";

                var retryAfter = 12;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterSpan))
                {
                    retryAfter = Math.Max(1, (int)retryAfterSpan.TotalSeconds);
                }

                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

                var response = new
                {
                    status = 429,
                    error = "rate_limit_exceeded",
                    message = "Rate limit reached. Please give Ankit's Digital Twin a brief moment before sending your next request.",
                    retryAfterSeconds = retryAfter
                };

                await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
            };

            // 1. Chat Policy: Max 5 questions / minute per User ID (or IP fallback)
            options.AddPolicy(ChatPolicy, httpContext =>
            {
                var partitionKey = GetUserOrIpPartitionKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 3,
                    QueueLimit = 0
                });
            });

            // 2. FollowUp Suggestions Policy: Max 10 calls / minute per User ID (or IP)
            options.AddPolicy(FollowUpPolicy, httpContext =>
            {
                var partitionKey = GetUserOrIpPartitionKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 2,
                    QueueLimit = 0
                });
            });

            // 3. Meeting Booking Policy: Max 2 bookings / hour per User/Email
            options.AddPolicy(BookingPolicy, httpContext =>
            {
                var partitionKey = GetUserOrIpPartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 2,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0
                });
            });

            // 4. Calendar Slots Lookup Policy: Max 20 lookups / minute per IP
            options.AddPolicy(CalendarSlotsPolicy, httpContext =>
            {
                var partitionKey = GetClientIp(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 2,
                    QueueLimit = 0
                });
            });

            // 5. Anonymous Endpoints Policy: Max 25 requests / minute per IP
            options.AddPolicy(AnonPolicy, httpContext =>
            {
                var partitionKey = GetClientIp(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 25,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });

            // 6. Global Concurrency Limiter: Max 5 concurrent streaming connections globally
            options.AddPolicy(ConcurrencyPolicy, _ =>
                RateLimitPartition.GetConcurrencyLimiter("global-concurrency", _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 5,
                    QueueLimit = 0
                }));
        });

        return services;
    }

    private static string GetUserOrIpPartitionKey(HttpContext httpContext)
    {
        var user = httpContext.User;
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                  user.FindFirst("sub")?.Value ??
                  user.FindFirst(ClaimTypes.Email)?.Value ??
                  user.FindFirst("email")?.Value;

        if (!string.IsNullOrWhiteSpace(sub))
        {
            return $"user:{sub}";
        }

        return $"ip:{GetClientIp(httpContext)}";
    }

    private static string GetClientIp(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var ip = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(ip)) return ip;
        }

        var cfConnectingIp = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cfConnectingIp)) return cfConnectingIp;

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
