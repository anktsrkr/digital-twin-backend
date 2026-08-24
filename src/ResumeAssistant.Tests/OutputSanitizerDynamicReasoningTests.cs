using Microsoft.Extensions.AI;
using ResumeAssistant.Api.Agent;
using Xunit;

namespace ResumeAssistant.Tests;

public class OutputSanitizerDynamicReasoningTests
{
    private sealed class EmptyTextWithReasoningChatClient(string reasoningText) : IChatClient
    {
        public ChatClientMetadata Metadata => new("EmptyTextWithReasoning");
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            var assistantMsg = new ChatMessage(ChatRole.Assistant, "");
            assistantMsg.Contents.Add(new TextContent(""));
            assistantMsg.Contents.Add(new CustomReasoningContent(reasoningText));
            return Task.FromResult(new ChatResponse(assistantMsg));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            var update = new ChatResponseUpdate
            {
                Role = ChatRole.Assistant
            };
            update.Contents.Add(new CustomReasoningContent(reasoningText));
            yield return update;
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class CustomReasoningContent(string text) : AIContent
    {
        public string Text { get; } = text;
        public override string ToString() => Text;
    }

    private sealed class CompletelyEmptyChatClient : IChatClient
    {
        public ChatClientMetadata Metadata => new("CompletelyEmpty");
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            yield return new ChatResponseUpdate { Role = ChatRole.Assistant };
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    [Fact]
    public async Task OutputSanitizer_ExtractsDynamicContent_FromReasoningTrace_WhenTextIsEmpty()
    {
        var reasoningTrace = """
            * User Question: "How do you implement human oversight for high-risk MCP tool execution?"
            * Context: Recruiter asking about MCP HITL patterns.
            * Persona: Ankit Sarkar, AI Solutions Architect.
            * Pillar 1: Orchestration/Control Plane Gatekeeper with pending approval state.
            * Pillar 2: Isolated Execution Sandbox with ephemeral microVMs.
            * Pillar 3: Zero-Trust Governance and ReBAC authorization checks.
            """;

        var innerClient = new EmptyTextWithReasoningChatClient(reasoningTrace);
        var sanitizer = new OutputSanitizerChatClient(innerClient);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "How do you implement human oversight for high-risk MCP tool execution?")
        };

        var response = await sanitizer.GetResponseAsync(messages);

        Assert.NotNull(response.Text);
        Assert.NotEmpty(response.Text);
        Assert.Contains("Orchestration/Control Plane", response.Text);
        Assert.Contains("Isolated Execution Sandbox", response.Text);
        Assert.Contains("Zero-Trust Governance", response.Text);
        Assert.DoesNotContain("User Question:", response.Text);
        Assert.DoesNotContain("Persona:", response.Text);
    }

    [Fact]
    public async Task OutputSanitizer_StreamsDynamicContent_FromReasoningTrace_WhenTextIsEmpty()
    {
        var reasoningTrace = """
            * User Question: "How do you implement human oversight for high-risk MCP tool execution?"
            * Pillar 1: Orchestration/Control Plane Gatekeeper with pending approval state.
            * Pillar 2: Isolated Execution Sandbox with ephemeral microVMs.
            * Pillar 3: Zero-Trust Governance and ReBAC authorization checks.
            """;

        var innerClient = new EmptyTextWithReasoningChatClient(reasoningTrace);
        var sanitizer = new OutputSanitizerChatClient(innerClient);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "How do you implement human oversight for high-risk MCP tool execution?")
        };

        var chunks = new List<ChatResponseUpdate>();
        await foreach (var chunk in sanitizer.GetStreamingResponseAsync(messages))
        {
            chunks.Add(chunk);
        }

        var fullText = string.Join("", chunks.Select(c => c.Text ?? string.Join("", c.Contents.OfType<TextContent>().Select(t => t.Text))));
        Assert.NotEmpty(fullText);
        Assert.Contains("Orchestration/Control Plane", fullText);
        Assert.Contains("Isolated Execution Sandbox", fullText);
    }

    [Fact]
    public async Task OutputSanitizer_ProvidesContextualFallback_WhenCompletelyEmpty()
    {
        var innerClient = new CompletelyEmptyChatClient();
        var sanitizer = new OutputSanitizerChatClient(innerClient);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "<recruiter_query>\nHow do you secure MCP tool execution in production?\n</recruiter_query>")
        };

        var response = await sanitizer.GetResponseAsync(messages);

        Assert.NotNull(response.Text);
        Assert.NotEmpty(response.Text);
        Assert.Contains("How do you secure MCP tool execution in production?", response.Text);
        Assert.Contains("Orchestration & Control Plane", response.Text);
        Assert.Contains("Zero-Trust Governance & ReBAC", response.Text);
    }

    [Fact]
    public async Task OutputSanitizer_Enforces8192TokenBudget()
    {
        var innerClient = new CompletelyEmptyChatClient();
        var sanitizer = new OutputSanitizerChatClient(innerClient);

        var options = new ChatOptions
        {
            MaxOutputTokens = 16384
        };

        await sanitizer.GetResponseAsync([new ChatMessage(ChatRole.User, "Test query")], options);

        Assert.NotNull(innerClient.LastOptions);
        Assert.Equal(8192, innerClient.LastOptions.MaxOutputTokens);
    }


    private sealed class RateLimitedChatClient : IChatClient
    {
        public ChatClientMetadata Metadata => new("RateLimited");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new System.ClientModel.ClientResultException("Service request failed. Status: 429 (Too Many Requests)", null);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new System.ClientModel.ClientResultException("Service request failed. Status: 429 (Too Many Requests)", null);
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    [Fact]
    public async Task OutputSanitizer_Handles429RateLimit_GracefullyInGetResponse()
    {
        var innerClient = new RateLimitedChatClient();
        var sanitizer = new OutputSanitizerChatClient(innerClient);

        var response = await sanitizer.GetResponseAsync([new ChatMessage(ChatRole.User, "Test query")]);

        Assert.NotNull(response.Text);
        Assert.Contains("rate limiting", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("brief moment", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OutputSanitizer_Handles429RateLimit_GracefullyInGetStreamingResponse()
    {
        var innerClient = new RateLimitedChatClient();
        var sanitizer = new OutputSanitizerChatClient(innerClient);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var u in sanitizer.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Test query")]))
        {
            updates.Add(u);
        }

        var fullText = string.Join("", updates.Select(c => c.Text ?? string.Join("", c.Contents.OfType<TextContent>().Select(t => t.Text))));
        Assert.NotEmpty(fullText);
        Assert.Contains("rate limiting", fullText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("brief moment", fullText, StringComparison.OrdinalIgnoreCase);
    }
}
