using Microsoft.Extensions.Logging.Abstractions;
using ResumeAssistant.Api.Agent;
using ResumeAssistant.Api.Configuration;
using Xunit;

namespace ResumeAssistant.Tests;

public class FollowUpAgentTests
{
    private static FollowUpAgent CreateAgent()
    {
        var options = new FollowUpLlmOptions
        {
            Mode = "local",
            Local = new LocalLlmConfig
            {
                Endpoint = "http://localhost:1234/v1",
                Model = "qwen2.5-coder-7b-instruct"
            }
        };

        return new FollowUpAgent(options, NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task FollowUpAgent_Turn10_FastPath_ReturnsConversionPillsWithoutLlm()
    {
        var agent = CreateAgent();
        var messages = new List<FollowUpMessageItem>
        {
            new() { Role = "user", Content = "Question 1" },
            new() { Role = "assistant", Content = "Answer 1" }
        };

        var response = await agent.GenerateFollowUpPillsAsync(messages, turnCount: 10, maxLimit: 10);

        Assert.NotNull(response);
        Assert.Equal(5, response.Pills.Count);

        // First 2 must be mandatory action pills
        Assert.Equal("action-download-resume", response.Pills[0].Id);
        Assert.Equal("action-book-call", response.Pills[1].Id);

        // Remaining 3 must be conversion action pills
        Assert.Contains(response.Pills, p => p.Id == "action-email-ankit");
        Assert.Contains(response.Pills, p => p.Id == "action-linkedin-github");
        Assert.Contains(response.Pills, p => p.Id == "action-availability-summary");
    }

    [Fact]
    public async Task FollowUpAgent_EmptySession_ReturnsDefaultStage1Pills()
    {
        var agent = CreateAgent();
        var response = await agent.GenerateFollowUpPillsAsync([], turnCount: 1, maxLimit: 10);

        Assert.NotNull(response);
        Assert.Equal(5, response.Pills.Count);
        Assert.Equal("action-download-resume", response.Pills[0].Id);
        Assert.Equal("action-book-call", response.Pills[1].Id);
        Assert.Contains(response.Pills, p => p.Id == "default-asda");
        Assert.Contains(response.Pills, p => p.Id == "default-agentic");
        Assert.Contains(response.Pills, p => p.Id == "default-visa");
    }

    [Fact]
    public async Task FollowUpAgent_LateTurnNudge_PillsReflectDueDiligenceDefaults()
    {
        var agent = CreateAgent();
        // Since local endpoint won't be reachable during pure unit test, it safely falls back to stage 3 defaults
        var messages = new List<FollowUpMessageItem>
        {
            new() { Role = "user", Content = "Tell me about your background" },
            new() { Role = "assistant", Content = "I am a Principal AI Engineer..." }
        };

        var response = await agent.GenerateFollowUpPillsAsync(messages, turnCount: 8, maxLimit: 10);

        Assert.NotNull(response);
        Assert.Equal(5, response.Pills.Count);
        Assert.Equal("action-download-resume", response.Pills[0].Id);
        Assert.Equal("action-book-call", response.Pills[1].Id);
        Assert.Contains(response.Pills, p => p.Category == "Logistics");
    }

    [Fact]
    public async Task FollowUpAgent_AntiLooping_ExcludesAlreadyAskedQuestion()
    {
        var agent = CreateAgent();
        var askedQuestion = "How did you achieve zero downtime during ASDA's 90k/30-min peak trading?";
        var messages = new List<FollowUpMessageItem>
        {
            new() { Role = "user", Content = askedQuestion },
            new() { Role = "assistant", Content = "We used blue-green deployments with canary traffic shifting..." }
        };

        var response = await agent.GenerateFollowUpPillsAsync(messages, turnCount: 2, maxLimit: 10);

        Assert.NotNull(response);
        // The exact asked question must NOT be suggested back to the user
        Assert.DoesNotContain(response.Pills, p => p.Prompt.Equals(askedQuestion, StringComparison.OrdinalIgnoreCase));
    }
}
