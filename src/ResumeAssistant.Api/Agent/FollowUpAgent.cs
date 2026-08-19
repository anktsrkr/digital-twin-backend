using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using ResumeAssistant.Api.Configuration;

namespace ResumeAssistant.Api.Agent;

public interface IFollowUpAgent
{
    Task<FollowUpResponse> GenerateFollowUpPillsAsync(
        IEnumerable<FollowUpMessageItem> messages,
        CancellationToken cancellationToken = default);
}

public sealed class FollowUpAgent : IFollowUpAgent
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<FollowUpAgent> _logger;

    public FollowUpAgent(
        FollowUpLlmOptions followUpOptions,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<FollowUpAgent>();
        _chatClient = LlmChatClientFactory.CreateChatClient(followUpOptions.ToLlmOptions(), _logger);
    }

    public async Task<FollowUpResponse> GenerateFollowUpPillsAsync(
        IEnumerable<FollowUpMessageItem> messages,
        CancellationToken cancellationToken = default)
    {
        // 1. Always create the 2 mandatory actionable recruiter pills first
        var pills = new List<FollowUpPillItem>
        {
            new()
            {
                Id = "action-download-resume",
                Label = "Download Resume",
                ActionType = "download_resume",
                Category = "Action",
                Icon = "file-text",
                Prompt = "Can I download Ankit Sarkar's resume PDF?"
            },
            new()
            {
                Id = "action-book-call",
                Label = "Book a Call",
                ActionType = "book_call",
                Category = "Action",
                Icon = "calendar",
                Prompt = "When is Ankit available for an interview?"
            }
        };

        // 2. Extract conversation context
        var messageList = messages?.ToList() ?? [];
        var recentRelevant = messageList
            .Where(m => !string.IsNullOrWhiteSpace(m.Content) && (m.Role == "user" || m.Role == "assistant"))
            .TakeLast(6)
            .ToList();

        if (recentRelevant.Count == 0)
        {
            // Initial / Empty state fallback questions (up to 3 questions -> total 5 pills)
            pills.AddRange(GetDefaultContextualQuestions());
            return new FollowUpResponse { Pills = pills };
        }

        try
        {
            var dynamicQuestions = await FetchContextualQuestionsFromLlmAsync(recentRelevant, cancellationToken);
            if (dynamicQuestions.Count > 0)
            {
                for (int i = 0; i < Math.Min(3, dynamicQuestions.Count); i++)
                {
                    var q = dynamicQuestions[i].Trim();
                    if (string.IsNullOrWhiteSpace(q)) continue;

                    pills.Add(new FollowUpPillItem
                    {
                        Id = $"followup-q-{i + 1}",
                        Label = FormatPillLabel(q),
                        ActionType = "ask_question",
                        Category = "Technical",
                        Icon = "sparkles",
                        Prompt = q
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate dynamic follow-up questions from LLM. Falling back to default suggestions.");
        }

        // If LLM returned fewer than 3 or failed, fill up to 5 with curated questions
        if (pills.Count < 5)
        {
            var defaults = GetDefaultContextualQuestions();
            foreach (var d in defaults)
            {
                if (pills.Count >= 5) break;
                if (!pills.Any(p => p.Prompt.Equals(d.Prompt, StringComparison.OrdinalIgnoreCase)))
                {
                    pills.Add(d);
                }
            }
        }

        // Strict cap at 5 pills
        return new FollowUpResponse
        {
            Pills = pills.Take(5).ToList()
        };
    }

    private async Task<List<string>> FetchContextualQuestionsFromLlmAsync(
        List<FollowUpMessageItem> recentMessages,
        CancellationToken cancellationToken)
    {
        var conversationSummary = string.Join("\n", recentMessages.Select(m => $"{m.Role.ToUpperInvariant()}: {m.Content}"));

        var systemPrompt = """
            You are an expert Technical Recruiter & Engineering Hiring Manager Assistant. You are screening Ankit Sarkar for a Principal AI Engineer / Solutions Architect role (13+ yrs experience, ASDA eCommerce Picking Platform 700k/wk, Microsoft Agent Framework, Model Context Protocol (MCP), SpiceDB ReBAC RAG, Boots UK, NMBS, UK Global Business Mobility Visa).

            Analyze the recent conversation between the interviewer (recruiter / hiring manager) and Ankit's Digital Twin.
            Generate 2 to 3 sharp, probing follow-up screening questions that a Director of Engineering, Chief Architect, or Lead Recruiter would naturally ask Ankit next to evaluate his seniority, technical depth, ownership, or hiring fit.

            Question Archetypes to draw from:
            - Architecture & Scale Depth: Probe specific failure modes, distributed state, concurrency, latency, or 0-downtime strategies.
            - Architectural Trade-offs & Tech Choices: Probe why he chose a specific technology (e.g. SpiceDB ReBAC vs RBAC, MCP vs custom REST, Semantic Kernel vs custom agent loops).
            - Leadership & Scope of Ownership: Probe his specific lead role vs individual contributor scope, mentoring, or multi-vendor team leadership.
            - Logistics & Due Diligence: If technical topics have been covered, probe 3-month notice period, UK Skilled Worker sponsorship transfer, or global remote preferences.

            Rules:
            1. Direct Candidate Address: Phrase every question in the second person ("you", "your architecture", "did you handle").
            2. Probing & Competency-Based: Avoid basic introductory questions (e.g. "What is AI?"). Ask high-conviction screening questions.
            3. Crisp & Punchy: Keep each question between 8 and 14 words.
            4. Return ONLY a valid JSON array of strings, for example:
               ["How did you achieve zero downtime during ASDA's 90k/30-min peak trading?", "How do you evaluate multi-agent orchestration reliability and tool security?", "What is your notice period and UK visa sponsorship timeline?"]
            5. Do not include markdown fences, code blocks, or extra text. Output ONLY the raw JSON array.
            """;

        var chatMessages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, $"Recent conversation history:\n{conversationSummary}\n\nProvide 2-3 follow-up screening questions as a JSON array of strings:")
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var response = await _chatClient.GetResponseAsync(chatMessages, new ChatOptions
        {
            Temperature = 0.3f,
        }, cts.Token);

        var rawText = response.Text?.Trim() ?? string.Empty;
        return ParseQuestionsFromJson(rawText);
    }

    private static List<string> ParseQuestionsFromJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return [];

        // Remove code block markdown if model included it
        var clean = Regex.Replace(rawJson, @"```json\s*|```\s*", string.Empty).Trim();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(clean);
            if (parsed is not null && parsed.Count > 0)
            {
                return parsed.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }
        }
        catch
        {
            // Regex fallback if JSON parsing failed
            var matches = Regex.Matches(clean, @"""([^""]{10,120}\??)""");
            if (matches.Count > 0)
            {
                return matches.Select(m => m.Groups[1].Value.Trim()).Where(s => s.Length > 5).Take(3).ToList();
            }
        }

        return [];
    }

    private static string FormatPillLabel(string question)
    {
        if (question.Length <= 45) return question;
        return question[..42].TrimEnd() + "...";
    }

    private static List<FollowUpPillItem> GetDefaultContextualQuestions() =>
    [
        new()
        {
            Id = "default-asda",
            Label = "ASDA Scale & Zero-Incident Resilience",
            ActionType = "ask_question",
            Category = "Flagship Scale",
            Icon = "sparkles",
            Prompt = "How did you achieve zero downtime during ASDA's 90k/30-min peak trading?"
        },
        new()
        {
            Id = "default-agentic",
            Label = "Agentic AI, MCP & Enterprise Security",
            ActionType = "ask_question",
            Category = "AI Architecture",
            Icon = "sparkles",
            Prompt = "How do you secure MCP tool calling and multi-agent workflows in production?"
        },
        new()
        {
            Id = "default-visa",
            Label = "Work Rights & Availability",
            ActionType = "ask_question",
            Category = "Authorisation",
            Icon = "sparkles",
            Prompt = "What is your UK visa status, notice period, and relocation / remote preference?"
        }
    ];
}
