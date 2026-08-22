using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace ResumeAssistant.Api.Agent;

/// <summary>
/// Post-LLM guardrail client that monitors model responses for canary token leaks,
/// scrubs internal function names, bounds output token budgets to 3072 on Cloudflare Workers AI,
/// and dynamically extracts architectural insights from reasoning traces if the model exhausts tokens during reasoning.
/// Preserves original ChatResponseUpdate.Contents for AG-UI streaming compatibility.
/// </summary>
public sealed partial class OutputSanitizerChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public const string SystemCanaryToken = "ANKEY_GUARD_TOKEN_7894";

    private static readonly Regex ToolNameScrubberRegex = new(
        @"\b(SearchResumeKnowledgeBase|BookInterviewSlot|GetAvailableInterviewSlots|ShowDownloadResumeCard|ShowScheduleInterviewCard)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private const string LeakedCanarySafeResponse =
        "As an AI Solutions Architect, I focus on delivering secure, production-grade cloud and agentic platforms. Feel free to explore my verified case studies (like ASDA peak resilience or enterprise MCP architectures) or schedule an interview below!";

    private static ChatOptions EnsureBoundedOptions(ChatOptions? options)
    {
        var bounded = options ?? new ChatOptions();
        bounded.MaxOutputTokens = Math.Min(bounded.MaxOutputTokens ?? 3072, 3072);
        bounded.Temperature ??= 0.25f;
        return bounded;
    }

    private static string SanitizeOutputText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        if (text.Contains(SystemCanaryToken, StringComparison.OrdinalIgnoreCase))
        {
            return LeakedCanarySafeResponse;
        }

        try
        {
            return ToolNameScrubberRegex.Replace(text, match => match.Value switch
            {
                var s when s.Equals("SearchResumeKnowledgeBase", StringComparison.OrdinalIgnoreCase) => "my verified case studies",
                var s when s.Equals("BookInterviewSlot", StringComparison.OrdinalIgnoreCase) => "the scheduling system",
                var s when s.Equals("GetAvailableInterviewSlots", StringComparison.OrdinalIgnoreCase) => "my calendar",
                var s when s.Equals("ShowDownloadResumeCard", StringComparison.OrdinalIgnoreCase) => "the resume download card",
                var s when s.Equals("ShowScheduleInterviewCard", StringComparison.OrdinalIgnoreCase) => "the meeting card",
                _ => "my system"
            });
        }
        catch (RegexMatchTimeoutException)
        {
            return text;
        }
    }

    private static string? ExtractReasoningText(AIContent? content)
    {
        if (content == null) return null;
        if (content is TextContent tc && !string.IsNullOrWhiteSpace(tc.Text))
        {
            return tc.Text;
        }

        var props = content.GetType().GetProperties();
        foreach (var p in props)
        {
            if (p.Name.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals("Reasoning", StringComparison.OrdinalIgnoreCase))
            {
                var val = p.GetValue(content)?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }

        return null;
    }

    private static string ExtractDynamicFallback(IEnumerable<ChatMessage> messages, string? reasoningAccumulated)
    {
        // 1. Try extracting substantive architectural points from reasoning trace
        if (!string.IsNullOrWhiteSpace(reasoningAccumulated))
        {
            var lines = reasoningAccumulated.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var meaningfulLines = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Skip internal chain-of-thought metadata prompts
                if (line.StartsWith("* User Question:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("User Question:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("* Context:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Context:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("* Persona:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Persona:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("* Goal:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Goal:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("* Constraint Check:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Constraint Check:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("* Wait,", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Wait,", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("<recruiter_query>", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("</recruiter_query>", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                meaningfulLines.Add(line);
            }

            if (meaningfulLines.Count >= 2)
            {
                var cleanedReasoning = string.Join("\n\n", meaningfulLines.Take(8));
                if (cleanedReasoning.Length > 80)
                {
                    return $"{cleanedReasoning}\n\nI would be delighted to dive deeper into these production patterns, operational trade-offs, or tool sandboxing controls during a technical screening call. Feel free to choose any slot on my calendar below!";
                }
            }
        }

        // 2. Dynamic synthesis tailored to the recruiter's prompt
        var lastUserMsg = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        lastUserMsg = Regex.Replace(lastUserMsg, @"</?recruiter_query>", string.Empty).Trim();
        if (lastUserMsg.Length > 90) lastUserMsg = lastUserMsg[..87] + "...";

        var topicSnippet = !string.IsNullOrWhiteSpace(lastUserMsg) ? $"\"{lastUserMsg}\"" : "your inquiry";

        return $"When implementing production-grade architecture for {topicSnippet}, I ground the design in three foundational enterprise pillars:\n\n" +
               "1. **Orchestration & Control Plane**: Intercepting high-risk operations via a policy engine, transitioning workflows into a persisted *Pending Approval* state, and awaiting human sign-off before dispatch.\n" +
               "2. **Zero-Trust Governance & ReBAC**: Enforcing fine-grained authorization (such as SpiceDB ReBAC) to validate caller identity, resource boundaries, and context before any tool execution.\n" +
               "3. **Isolated Execution Sandbox**: Running untrusted agent actions and external tools in isolated, ephemeral containers with strict outbound egress controls to neutralize prompt injection escapes.\n\n" +
               "I'd be glad to walk through the implementation trade-offs and real-world architectures on a technical screening call. Please feel free to pick any slot on my calendar below!";
    }

    private const string RateLimitFallbackMessage =
        "My AI compute engine (Cloudflare Workers AI) is currently experiencing high query demand and rate limiting. Please give me a brief moment (10–12 seconds) before sending your next inquiry, or feel free to book a direct conversation slot on my calendar below!";

    private static bool IsRateLimitException(Exception ex)
    {
        if (ex is System.ClientModel.ClientResultException cre && cre.Status == 429) return true;
        if (ex is HttpRequestException hre && hre.StatusCode == System.Net.HttpStatusCode.TooManyRequests) return true;
        var msg = ex.Message ?? "";
        return msg.Contains("429") || msg.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var boundedOptions = EnsureBoundedOptions(options);
        ChatResponse response;
        try
        {
            response = await base.GetResponseAsync(messages, boundedOptions, cancellationToken);
        }
        catch (Exception ex) when (IsRateLimitException(ex))
        {
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, RateLimitFallbackMessage));
        }
        catch (Exception)
        {
            var dynamicFallback = ExtractDynamicFallback(messages, null);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, SanitizeOutputText(dynamicFallback)));
        }

        var hasAnyText = false;
        var hasAnyToolCall = false;
        var reasoningBuilder = new StringBuilder();

        if (response.Messages is not null)
        {
            for (int i = 0; i < response.Messages.Count; i++)
            {
                var msg = response.Messages[i];
                if (!string.IsNullOrWhiteSpace(msg.Text))
                {
                    hasAnyText = true;
                    var sanitized = SanitizeOutputText(msg.Text);
                    if (!string.Equals(sanitized, msg.Text, StringComparison.Ordinal))
                    {
                        var newContents = new List<AIContent>();
                        bool replaced = false;
                        foreach (var c in msg.Contents)
                        {
                            if (c is TextContent && !replaced)
                            {
                                newContents.Add(new TextContent(sanitized));
                                replaced = true;
                            }
                            else
                            {
                                newContents.Add(c);
                            }
                        }
                        if (!replaced) newContents.Add(new TextContent(sanitized));

                        response.Messages[i] = new ChatMessage(msg.Role, newContents);
                    }
                }

                if (msg.Contents is not null)
                {
                    foreach (var c in msg.Contents)
                    {
                        if (c is FunctionCallContent)
                        {
                            hasAnyToolCall = true;
                        }
                        else if (c is not TextContent)
                        {
                            var rText = ExtractReasoningText(c);
                            if (!string.IsNullOrWhiteSpace(rText)) reasoningBuilder.AppendLine(rText);
                        }
                    }
                }
            }
        }

        if (!hasAnyText && !hasAnyToolCall)
        {
            var dynamicText = ExtractDynamicFallback(messages, reasoningBuilder.ToString());
            var sanitized = SanitizeOutputText(dynamicText);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, sanitized));
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var boundedOptions = EnsureBoundedOptions(options);
        var textEmitted = false;
        var toolCallEmitted = false;
        var reasoningAccumulator = new StringBuilder();
        string? errorFallbackText = null;

        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        try
        {
            var stream = base.GetStreamingResponseAsync(messages, boundedOptions, cancellationToken);
            enumerator = stream.GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception ex) when (IsRateLimitException(ex))
        {
            errorFallbackText = RateLimitFallbackMessage;
        }
        catch (Exception)
        {
            errorFallbackText = SanitizeOutputText(ExtractDynamicFallback(messages, null));
        }

        if (errorFallbackText != null)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, errorFallbackText);
            yield break;
        }

        try
        {
            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync()) break;
                    update = enumerator.Current;
                }
                catch (Exception ex) when (IsRateLimitException(ex))
                {
                    if (!textEmitted)
                    {
                        errorFallbackText = RateLimitFallbackMessage;
                    }
                    break;
                }
                catch (Exception)
                {
                    if (!textEmitted)
                    {
                        errorFallbackText = SanitizeOutputText(ExtractDynamicFallback(messages, reasoningAccumulator.ToString()));
                    }
                    break;
                }

                if (update.Contents is not null)
                {
                    foreach (var c in update.Contents)
                    {
                        if (c is FunctionCallContent)
                        {
                            toolCallEmitted = true;
                        }
                        else if (c is not TextContent)
                        {
                            var rText = ExtractReasoningText(c);
                            if (!string.IsNullOrWhiteSpace(rText)) reasoningAccumulator.AppendLine(rText);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(update.Text))
                {
                    textEmitted = true;
                    if (update.Text.Contains(SystemCanaryToken, StringComparison.OrdinalIgnoreCase))
                    {
                        var canaryReplacement = new ChatResponseUpdate
                        {
                            Role = ChatRole.Assistant
                        };
                        canaryReplacement.Contents.Add(new TextContent(LeakedCanarySafeResponse));
                        yield return canaryReplacement;
                        yield break;
                    }

                    var sanitized = SanitizeOutputText(update.Text);
                    if (string.Equals(sanitized, update.Text, StringComparison.Ordinal))
                    {
                        yield return update;
                    }
                    else
                    {
                        var modified = new ChatResponseUpdate
                        {
                            Role = update.Role,
                            FinishReason = update.FinishReason,
                            RawRepresentation = update.RawRepresentation,
                            ResponseId = update.ResponseId
                        };
                        modified.Contents.Add(new TextContent(sanitized));
                        yield return modified;
                    }
                }
                else
                {
                    yield return update;
                }
            }
        }
        finally
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync();
            }
        }

        if (errorFallbackText != null)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, errorFallbackText);
            yield break;
        }

        // If stream ended with zero user-facing text and no tool calls, yield dynamic reasoning fallback
        if (!textEmitted && !toolCallEmitted)
        {
            var dynamicText = ExtractDynamicFallback(messages, reasoningAccumulator.ToString());
            var sanitized = SanitizeOutputText(dynamicText);

            var fallbackUpdate = new ChatResponseUpdate
            {
                Role = ChatRole.Assistant
            };
            fallbackUpdate.Contents.Add(new TextContent(sanitized));
            yield return fallbackUpdate;
        }
    }
}
