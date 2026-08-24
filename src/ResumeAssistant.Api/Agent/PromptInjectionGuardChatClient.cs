using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace ResumeAssistant.Api.Agent;

/// <summary>
/// Pre-LLM guardrail client that inspects incoming user queries for prompt injection,
/// jailbreak attempts, and system prompt extraction attacks before they reach the model.
/// Also encapsulates clean user queries inside &lt;recruiter_query&gt; delimiters for Gemma 4.
/// </summary>
public sealed partial class PromptInjectionGuardChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    private static readonly Regex DirectJailbreakRegex = new(
        @"(?:ignore|disregard|forget)\s+(?:all\s+)?(?:previous|prior|above)\s+(?:instructions|rules|prompts|directives)|(?:you\s+are\s+now|act\s+as)\s+(?:DAN|unrestricted|a\s+jailbroken|a\s+linux\s+terminal|root)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private static readonly Regex SystemPromptExtractionRegex = new(
        @"(?:print|reveal|output|dump|show|repeat)\s+(?:all\s+)?(?:the\s+|your\s+)?(?:exact\s+)?(?:system\s+prompt|system\s+instructions|internal\s+tools|developer\s+mode|secret\s+canary)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private const string SafeRefusalMessage =
        "I am Ankit Sarkar's Digital Twin, focused on technical screening, architectural discussions, and interview availability. I maintain strict professional boundaries and do not process prompt overrides or system instruction extraction requests. Feel free to ask about my engineering experience (such as high-scale retail peak resilience or MCP architectures) or schedule an interview below!";

    private static bool IsAdversarialInjection(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            return DirectJailbreakRegex.IsMatch(text) || SystemPromptExtractionRegex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static IEnumerable<ChatMessage> SanitizeAndEncapsulate(IEnumerable<ChatMessage> messages, out bool isBlocked)
    {
        var msgList = messages.ToList();
        isBlocked = false;

        for (int i = 0; i < msgList.Count; i++)
        {
            var msg = msgList[i];
            if (msg.Role == ChatRole.User && !string.IsNullOrWhiteSpace(msg.Text))
            {
                if (IsAdversarialInjection(msg.Text))
                {
                    isBlocked = true;
                    return msgList;
                }

                // Encapsulate with XML delimiters for Gemma 4 role isolation if not already encapsulated
                if (!msg.Text.StartsWith("<recruiter_query>", StringComparison.OrdinalIgnoreCase))
                {
                    msgList[i] = new ChatMessage(ChatRole.User, $"<recruiter_query>\n{msg.Text.Trim()}\n</recruiter_query>");
                }
            }
        }

        return msgList;
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var processed = SanitizeAndEncapsulate(messages, out var isBlocked);
        if (isBlocked)
        {
            var refusalResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, SafeRefusalMessage));
            return Task.FromResult(refusalResponse);
        }

        return base.GetResponseAsync(processed, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var processed = SanitizeAndEncapsulate(messages, out var isBlocked);
        if (isBlocked)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, SafeRefusalMessage);
            yield break;
        }

        await foreach (var update in base.GetStreamingResponseAsync(processed, options, cancellationToken))
        {
            yield return update;
        }
    }
}
