using Microsoft.Extensions.Logging.Abstractions;
using ResumeAssistant.Api.Services;
using Xunit;

namespace ResumeAssistant.Tests;

public class DailyQuotaServiceTests
{
    [Fact]
    public async Task DailyQuota_IncrementsCorrectly_AndNudgesAt8And9_AndBlocksAt10()
    {
        var service = new MongoDbDailyQuotaService(null, NullLogger<MongoDbDailyQuotaService>.Instance);
        var testUserId = $"recruiter_test_{Guid.NewGuid():N}";
        var email = "recruiter@techcompany.com";

        // Turns 1 to 7: Allowed = true, IsNudgeThreshold = false
        for (int i = 1; i <= 7; i++)
        {
            var result = await service.CheckAndIncrementAsync(testUserId, email, isExemptAction: false);
            Assert.True(result.Allowed, $"Turn {i} should be allowed.");
            Assert.Equal(i, result.CurrentCount);
            Assert.Equal(10 - i, result.Remaining);
            Assert.False(result.IsNudgeThreshold, $"Turn {i} should not be a nudge threshold.");
        }

        // Turn 8: Allowed = true, IsNudgeThreshold = true
        var turn8 = await service.CheckAndIncrementAsync(testUserId, email, isExemptAction: false);
        Assert.True(turn8.Allowed);
        Assert.Equal(8, turn8.CurrentCount);
        Assert.Equal(2, turn8.Remaining);
        Assert.True(turn8.IsNudgeThreshold);

        // Turn 9: Allowed = true, IsNudgeThreshold = true
        var turn9 = await service.CheckAndIncrementAsync(testUserId, email, isExemptAction: false);
        Assert.True(turn9.Allowed);
        Assert.Equal(9, turn9.CurrentCount);
        Assert.Equal(1, turn9.Remaining);
        Assert.True(turn9.IsNudgeThreshold);

        // Turn 10: CurrentCount = 10, Remaining = 0, Allowed = true (this is the 10th and final question)
        var turn10 = await service.CheckAndIncrementAsync(testUserId, email, isExemptAction: false);
        Assert.True(turn10.Allowed);
        Assert.Equal(10, turn10.CurrentCount);
        Assert.Equal(0, turn10.Remaining);

        // Turn 11+: Blocked!
        var turn11 = await service.CheckAndIncrementAsync(testUserId, email, isExemptAction: false);
        Assert.False(turn11.Allowed, "Turn 11 should not be allowed.");
        Assert.Equal(11, turn11.CurrentCount);
        Assert.Equal(0, turn11.Remaining);
    }

    [Fact]
    public async Task DailyQuota_ExemptActions_DoNotIncrementQuota()
    {
        var service = new MongoDbDailyQuotaService(null, NullLogger<MongoDbDailyQuotaService>.Instance);
        var testUserId = $"recruiter_test_{Guid.NewGuid():N}";
        var email = "recruiter@hiring.com";

        // Initial question (turn 1)
        var initial = await service.CheckAndIncrementAsync(testUserId, email, isExemptAction: false);
        Assert.Equal(1, initial.CurrentCount);

        // Exempt action 1: Download Resume
        var resumeDownload = await service.CheckAndIncrementAsync(testUserId, email, isExemptAction: true);
        Assert.Equal(1, resumeDownload.CurrentCount); // Did not increment
        Assert.Equal(9, resumeDownload.Remaining);

        // Exempt action 2: Calendar Slot Check
        var calendarCheck = await service.CheckAndIncrementAsync(testUserId, email, isExemptAction: true);
        Assert.Equal(1, calendarCheck.CurrentCount); // Still 1

        // Next actual question (turn 2)
        var nextQuestion = await service.CheckAndIncrementAsync(testUserId, email, isExemptAction: false);
        Assert.Equal(2, nextQuestion.CurrentCount);
        Assert.Equal(8, nextQuestion.Remaining);
    }
}
