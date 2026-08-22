using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using ResumeAssistant.Api.Configuration;

namespace ResumeAssistant.Api.Agent;

public interface IFollowUpAgent
{
    Task<FollowUpResponse> GenerateFollowUpPillsAsync(
        IEnumerable<FollowUpMessageItem> messages,
        int turnCount = 1,
        int maxLimit = 10,
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
        int turnCount = 1,
        int maxLimit = 10,
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

        var messageList = messages?.ToList() ?? [];
        var pastUserQueries = messageList
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m.Content.Trim())
            .ToList();

        var effectiveTurn = turnCount > 0 ? turnCount : Math.Max(1, pastUserQueries.Count);

        // Fast-path: When quota limit (turn 10+) is reached, return conversion pills with 0 LLM token cost
        if (effectiveTurn >= maxLimit)
        {
            pills.AddRange(GetQuotaConversionPills());
            return new FollowUpResponse { Pills = pills.Take(5).ToList() };
        }

        // Empty session state: return initial curated questions
        if (pastUserQueries.Count == 0)
        {
            pills.AddRange(GetDefaultContextualQuestions(effectiveTurn));
            return new FollowUpResponse { Pills = pills.Take(5).ToList() };
        }

        try
        {
            var dynamicQuestions = await FetchContextualQuestionsFromLlmAsync(
                pastUserQueries,
                messageList,
                effectiveTurn,
                maxLimit,
                cancellationToken);

            if (dynamicQuestions.Count > 0)
            {
                for (int i = 0; i < Math.Min(3, dynamicQuestions.Count); i++)
                {
                    var q = dynamicQuestions[i].Trim();
                    if (string.IsNullOrWhiteSpace(q)) continue;

                    // Skip if prompt matches a question already asked by the user
                    if (pastUserQueries.Any(past => past.Equals(q, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    pills.Add(new FollowUpPillItem
                    {
                        Id = $"followup-q-{i + 1}",
                        Label = FormatPillLabel(q),
                        ActionType = "ask_question",
                        Category = DetermineCategory(effectiveTurn),
                        Icon = "sparkles",
                        Prompt = q
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate dynamic follow-up questions from LLM for turn {Turn}. Falling back to stage suggestions.", effectiveTurn);
        }

        // If LLM returned fewer than 3 or failed, fill up to 5 with stage-appropriate curated questions
        if (pills.Count < 5)
        {
            var defaults = GetDefaultContextualQuestions(effectiveTurn);
            foreach (var d in defaults)
            {
                if (pills.Count >= 5) break;
                if (!pills.Any(p => p.Prompt.Equals(d.Prompt, StringComparison.OrdinalIgnoreCase)) &&
                    !pastUserQueries.Any(u => u.Equals(d.Prompt, StringComparison.OrdinalIgnoreCase)))
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
        List<string> pastUserQueries,
        List<FollowUpMessageItem> messageList,
        int currentTurn,
        int maxLimit,
        CancellationToken cancellationToken)
    {
        // Context Compaction:
        // 1. Compact list of already asked topics (anti-looping negative constraint)
        var coveredTopicsManifest = pastUserQueries.Count > 0
            ? string.Join("\n", pastUserQueries.Select(q => $"- {q}"))
            : "- None yet";

        // 2. Immediate last turn only (truncated assistant response to max 280 chars)
        var lastUserMsg = messageList.FindLast(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content?.Trim() ?? string.Empty;
        var lastAssistantMsg = messageList.FindLast(m => m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))?.Content?.Trim() ?? string.Empty;
        if (lastAssistantMsg.Length > 280)
        {
            lastAssistantMsg = lastAssistantMsg[..277] + "...";
        }

        // Stage-Aware Prompt Guidance
        var stageGuidance = currentTurn switch
        {
            <= 3 => """
                Current Screening Phase: Stage 1 (Breadth & Flagship Impact).
                Focus on probing massive enterprise scale, high concurrency, and zero-downtime reliability (e.g. ASDA 700k/wk picking, 90k/30-min peak trading, distributed architecture).
                """,
            >= 8 => """
                Current Screening Phase: Stage 3 (Due Diligence & Hiring Logistics).
                The recruiter is near the 10-question daily quota. Focus on probing candidate logistics, 3-month notice period, UK Skilled Worker visa sponsorship transfer, remote/London hybrid preference, or direct interview preparation.
                """,
            _ => """
                Current Screening Phase: Stage 2 (Architectural Trade-offs & Leadership Depth).
                Focus on probing technical decision trade-offs (e.g. SpiceDB ReBAC vs RBAC, MCP tool security, multi-agent orchestration reliability, team mentoring vs IC scope).
                """
        };

        var systemPrompt = $"""
            You are an expert Technical Recruiter & Engineering Hiring Manager Assistant screening Ankit Sarkar for a Principal AI Engineer / Solutions Architect role (13+ yrs experience, ASDA eCommerce Picking Platform 700k/wk, Microsoft Agent Framework, Model Context Protocol (MCP), SpiceDB ReBAC RAG, Boots UK, NMBS, UK Global Business Mobility / Skilled Worker Visa, 3-month notice period).

            {stageGuidance}

            Negative Constraint (Anti-Looping):
            Do NOT repeat or suggest questions overlapping with topics already explored in this session:
            {coveredTopicsManifest}

            Rules:
            1. Direct Candidate Address: Phrase every question in the second person ("you", "your architecture", "did you handle").
            2. High Conviction: Avoid generic introductory questions (e.g. "What is AI?"). Ask sharp, senior-level screening questions.
            3. Crisp & Punchy: Keep each question between 8 and 14 words.
            4. Output ONLY a valid JSON array of 2 to 3 strings. Example:
               ["How did you achieve zero downtime during ASDA's 90k/30-min peak trading?", "How do you evaluate multi-agent orchestration reliability and tool security?"]
            5. Do NOT include markdown code blocks, backticks, or explanatory text. Output ONLY the raw JSON array.
            """;

        var userPrompt = $"""
            Session Progress: Question {currentTurn} of {maxLimit}

            Latest Conversation Turn:
            USER: {lastUserMsg}
            ASSISTANT: {lastAssistantMsg}

            Provide 2 to 3 follow-up screening questions as a JSON array of strings:
            """;

        var chatMessages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
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

    private static string DetermineCategory(int turn) => turn switch
    {
        <= 3 => "Flagship Scale",
        >= 8 => "Recruiter Diligence",
        _ => "AI Architecture"
    };

    private static List<FollowUpPillItem> GetQuotaConversionPills() =>
    [
        new()
        {
            Id = "action-email-ankit",
            Label = "Email Ankit Directly",
            ActionType = "ask_question",
            Category = "Direct Contact",
            Icon = "sparkles",
            Prompt = "How can I email Ankit Sarkar directly for an interview invitation?"
        },
        new()
        {
            Id = "action-linkedin-github",
            Label = "LinkedIn & GitHub Links",
            ActionType = "ask_question",
            Category = "Profiles",
            Icon = "sparkles",
            Prompt = "Can you share Ankit Sarkar's LinkedIn and GitHub profile links?"
        },
        new()
        {
            Id = "action-availability-summary",
            Label = "Availability & Notice Period",
            ActionType = "ask_question",
            Category = "Logistics",
            Icon = "sparkles",
            Prompt = "What is Ankit's UK visa status, 3-month notice period, and interview availability?"
        }
    ];

    private static List<FollowUpPillItem> GetDefaultContextualQuestions(int turn) => turn switch
    {
        >= 8 =>
        [
            new()
            {
                Id = "stage-visa",
                Label = "UK Visa Sponsorship Transfer",
                ActionType = "ask_question",
                Category = "Logistics",
                Icon = "sparkles",
                Prompt = "What is your UK visa sponsorship status and earliest start date?"
            },
            new()
            {
                Id = "stage-notice",
                Label = "3-Month Notice Period & Flexibility",
                ActionType = "ask_question",
                Category = "Logistics",
                Icon = "sparkles",
                Prompt = "Can you describe your 3-month notice period and interview availability?"
            },
            new()
            {
                Id = "stage-location",
                Label = "London Hybrid vs Remote Fit",
                ActionType = "ask_question",
                Category = "Logistics",
                Icon = "sparkles",
                Prompt = "What are your location preferences for London hybrid or remote roles?"
            }
        ],
        >= 4 =>
        [
            new()
            {
                Id = "stage-rebac",
                Label = "SpiceDB ReBAC RAG vs RBAC",
                ActionType = "ask_question",
                Category = "AI Architecture",
                Icon = "sparkles",
                Prompt = "Why did you choose SpiceDB ReBAC over traditional RBAC for enterprise RAG?"
            },
            new()
            {
                Id = "stage-mcp-security",
                Label = "MCP Tool Calling Security",
                ActionType = "ask_question",
                Category = "AI Architecture",
                Icon = "sparkles",
                Prompt = "How do you secure Model Context Protocol tool calling against prompt injection?"
            },
            new()
            {
                Id = "stage-leadership",
                Label = "Lead Architect Scope & Mentoring",
                ActionType = "ask_question",
                Category = "Leadership",
                Icon = "sparkles",
                Prompt = "How do you lead cross-functional engineering teams and mentor senior engineers?"
            }
        ],
        _ =>
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
        ]
    };
}
