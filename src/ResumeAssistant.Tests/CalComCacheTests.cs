using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ResumeAssistant.Api.Configuration;
using ResumeAssistant.Api.Services;
using Xunit;

namespace ResumeAssistant.Tests;

public class CalComCacheTests
{
    [Fact]
    public async Task CalComService_UsesMemoryCache_ForSlotsLookup()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var options = new CalComOptions
        {
            ApiKey = "test_key",
            Username = "ankitsarkar",
            EventTypeId30Min = 12345
        };

        var handler = new DelegatingMockHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cal.com/v2/") };

        var service = new CalComService(httpClient, options, NullLogger<CalComService>.Instance, memoryCache);

        var start = DateTime.UtcNow.Date.AddDays(1);
        var end = start.AddDays(5);

        // First call - triggers schedule generation / HTTP lookup
        var result1 = await service.GetAvailableSlotsAsync(start, end, "Europe/London", 30);
        Assert.NotNull(result1);
        Assert.True(result1.Success);

        // Verify it was cached in memory
        var expectedCacheKey = $"cal_slots_12345_{start:yyyy-MM-dd}_{end:yyyy-MM-dd}_Europe/London_30";
        Assert.True(memoryCache.TryGetValue(expectedCacheKey, out CalAvailabilityResponse? cached));
        Assert.NotNull(cached);
        Assert.Equal(result1.TotalSlotsFound, cached.TotalSlotsFound);

        // Second call with identical params returns immediately from cache
        var result2 = await service.GetAvailableSlotsAsync(start, end, "Europe/London", 30);
        Assert.Same(cached, result2);
    }

    private sealed class DelegatingMockHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"success\",\"data\":{\"slots\":{}}}")
            };
            return Task.FromResult(response);
        }
    }
}
