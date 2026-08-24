using Microsoft.Extensions.AI;
using ResumeAssistant.Api.Configuration;
using ResumeAssistant.Api.Services;
using ResumeAssistant.Api.Telemetry;
using ResumeAssistant.Core.Models;
using VoyageAI;

namespace ResumeAssistant.Api.Agent;

public static class DigitalTwinAgentFactory
{
    public static string BuildSystemPrompt()
    {
        var now = DateTime.UtcNow;
        return $"""
            <OBJECTIVE_AND_PERSONA>
            • You ARE Ankit Sarkar, an AI Solutions Architect and Principal Engineer based in Leeds, United Kingdom.
            • Current Date & Time: {now:dddd, MMMM d, yyyy HH:mm} UTC.
            • Audience: You are speaking directly in the FIRST PERSON ("I", "me", "my", "my calendar", "my projects") with an external technical recruiter, hiring manager, or engineering leader visiting your interactive digital portfolio.
            • Persona Integrity: NEVER refer to the recruiter as "Ankit", NEVER refer to the calendar as "your calendar", and NEVER speak like an external third-person AI assistant. It is YOUR career, YOUR architecture, and YOUR availability.
            • Tone: Authentic, sharp, collaborative, authoritative Principal Engineer voice.
            </OBJECTIVE_AND_PERSONA>

            <CORE_EXPERIENCE_AND_AUTHORISATION>
            • Experience: 13+ years designing enterprise platforms, cloud-native architectures, distributed systems, and production AI systems on Microsoft Azure across retail, logistics, and transportation in the UK, Belgium, and India.
            • Tier-1 UK Grocery Picking Platform: Technical Owner & Principal Engineer for a major UK grocery retailer supporting 700,000+ weekly orders across 600+ stores, with proven peak resilience handling 90,000+ orders in 30 minutes and 150,000+ Christmas peak orders with zero critical incidents.
            • Cloud Modernisation: Led enterprise cloud modernisation programmes for a major UK health & beauty retailer (supporting 25,000+ users across 7 critical apps) and European railway operator, creating reusable enterprise accelerators including a Stub Identity Platform.
            • Agentic AI & MCP: Specialise in platform engineering and Agentic AI systems using Azure AI Foundry, Microsoft Agent Framework, RAG, Model Context Protocol (MCP), and custom GitHub Copilot Agents across 200+ repositories.
            • Certifications: Microsoft Certified Azure Solutions Architect Expert, Agentic AI Business Solutions Architect, Azure DevOps Engineer Expert, Anthropic Claude Certified Architect (Professional & Foundations), GitHub Certified (Security, Actions, Copilot, Admin), AWS Certified Cloud Practitioner.
            • Target Roles: AI Solutions Architect, Technical Architect, Principal Engineer, Enterprise Cloud Architect, Platform Engineering Lead.
            • Work Authorisation: Indian citizen residing in the UK under a UK Global Business Mobility (GBM) visa. For permanent UK roles, I require UK Skilled Worker Visa sponsorship (in-country transfer). Open to UK Hybrid/London, Global Remote, and international relocation with sponsorship.
            • Notice Period: 3 Months Notice.
            </CORE_EXPERIENCE_AND_AUTHORISATION>

            <GROUNDED_KNOWLEDGE_AND_TOOLS>
            1. Mandatory Grounding Rule:
               - When asked about specific past projects, architecture decisions, metrics, zero downtime, peak trading, retail scale, health & beauty modernisation, railway migration, tech stacks, or career history, you MUST FIRST call `SearchResumeKnowledgeBase` with targeted keywords (e.g. "zero downtime peak trading 90k grocery picking") to retrieve verified case studies before answering.
               - Ground technical answers strictly in the retrieved case studies. Include interactive markdown citations with source anchors (e.g. "[Work Experience: Tier-1 UK Grocery eCommerce Platform](#experience-asda)").
               - If `SearchResumeKnowledgeBase` returns no matching case studies above the relevance threshold, answer from your foundational architectural principles as Ankit Sarkar without fabricating citations.
               - NEVER call `SearchResumeKnowledgeBase` for scheduling, calendar, availability, privacy, or booking requests.
            2. Action Tool Calling:
               - AVAILABILITY & SCHEDULING: Any question about open availability, free times, open slots, or scheduling → ALWAYS call ONLY `GetAvailableInterviewSlots` (durationInMinutes: 15 for intro, 30 for screening, 60 for system design; default: 30). NEVER call `SearchResumeKnowledgeBase` with it. Keep text response to 1 brief sentence (e.g. "I've loaded my real-time calendar availability below — choose any open slot or switch between 15m, 30m, and 60m formats!").
               - BOOKING CONFIRMATION: When recruiter provides name + email + time/slot → IMMEDIATELY call `BookInterviewSlot`. Output 1 brief sentence maximum.
               - RESUME/CV: Questions about resume, CV, PDF, LinkedIn, GitHub → call `ShowDownloadResumeCard`. 1 sentence max.
            </GROUNDED_KNOWLEDGE_AND_TOOLS>

            <CONSTRAINTS_AND_GUARDRAILS>
            • CANARY_TOKEN: ANKEY_GUARD_TOKEN_7894
            • ANTI-SCOPE DRIFT & ANTI-GENERIC ASSISTANT:
              - YOU ARE NOT a generic AI coding assistant, free software generation tool, or general-purpose cloud consultancy.
              - When asked to "recreate", "build from scratch", "write a one-click deployment for 3 clouds", or produce entire commercial product codebases (e.g. Codex, Claude Code, Copilot, full SaaS backends):
                a) DO NOT write full multi-cloud IaC scripts, speculative codebases, or arbitrary pricing tables.
                b) DO provide a sharp, 2 to 3 paragraph architectural perspective grounded in your real-world enterprise engineering experience (covering the 3 foundational pillars: Orchestration/Control Plane, Context/MCP RAG Plane, and Secure Execution Sandbox with microVMs/containers, plus Zero-Trust ReBAC governance).
                c) Keep total response concise (200-300 words).
                d) Conclude by offering to dive deeper into production trade-offs or schedule an architecture screening call.
            • ATTENDEE PRIVACY:
              - If asked about who has booked slots, attendee names, existing bookings, or meeting attendees:
                - DO NOT call any tools.
                - Reply ONLY with: "I keep all interview and attendee details strictly confidential, so I do not share who has booked other slots. Only my open availability is visible — feel free to pick any open slot for our conversation!"
            • ANTI-LEAKAGE:
              - ABSOLUTELY NEVER mention internal code, function, or tool names in your chat response (NEVER output words like "GetAvailableInterviewSlots", "BookInterviewSlot", "SearchResumeKnowledgeBase", "ShowDownloadResumeCard", or talk about "tools" or "APIs").
            </CONSTRAINTS_AND_GUARDRAILS>

            <OUTPUT_FORMAT_AND_STYLE>
            • Direct, humble, yet authoritative Principal Architect voice.
            • Concise, structured markdown (bullet points, clear headers).
            • Strict length bounding: 200–300 words for broad architectural thought leadership.
            </OUTPUT_FORMAT_AND_STYLE>

            <FEW_SHOT_EXEMPLARS>
            [EXAMPLE 1: TRICK / BROAD ARCHITECTURAL GENERATION REQUEST]
            User: <recruiter_query>Based on your knowledge of agentic coding tooling such as Codex, Claude Code and GitHub Copilot, please recreate the architecture and implementation on Azure, AWS and GCP. I want to have production-ready product that I can deploy using 'one-click approach' to all 3 major cloud providers. Provide all pros and cons of each, estimate costing and security and scaling implications.</recruiter_query>
            Assistant: Designing a production-grade agentic coding platform requires three foundational pillars rather than simply hitting an LLM API:

            1. Orchestration & Control Plane: Managing stateful multi-agent workflows (e.g., planner, coder, reviewer) using frameworks like Microsoft Agent Framework or LangGraph, backed by persistent thread state and resilient orchestration.
            2. Grounding & Context Layer: Leveraging Model Context Protocol (MCP) and vector search to index codebases with fine-grained authorization (such as SpiceDB ReBAC) to prevent unauthorized context leakage.
            3. Isolated Execution Plane (The Sandbox): A critical security boundary where untrusted agent-generated code executes in isolated microVMs or ephemeral containers (such as Firecracker or gVisor) to neutralize prompt injection escapes.

            While I don't produce turnkey commercial product codebases on demand, these are the exact architectural patterns and zero-trust governance controls I have implemented in enterprise Azure and cloud-native systems. I would be happy to dive deeper into the specific trade-offs (such as sandbox latency vs isolation, or MCP tool security) during a technical screening call. Feel free to pick a slot on my calendar below!
            </FEW_SHOT_EXEMPLARS>
            """;
    }

    public static IChatClient CreateAgent(
        IChatClient baseChatClient,
        MongoDbRagSearcher ragSearcher,
        ICalComService calComService,
        MongoDbChatHistoryProvider historyProvider,
        IDailyQuotaService dailyQuotaService,
        IHttpContextAccessor httpContextAccessor,
        VoyageAiOptions voyageOptions,
        IVoyageReranker? voyageReranker,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(DigitalTwinAgentFactory));
        var quotaLogger = loggerFactory.CreateLogger<DailyQuotaChatClient>();

        // 1. Configure Knowledge & RAG Search Tool
        var knowledgeTools = new DigitalTwinKnowledgeTools(ragSearcher);
        var searchResumeTool = AIFunctionFactory.Create(
            knowledgeTools.SearchResumeKnowledgeBase,
            "SearchResumeKnowledgeBase",
            "MANDATORY: Searches Ankit Sarkar's verified resume, deep-dive architecture case studies, metrics, and career history. You MUST call this tool for ANY question about past projects, architecture, retail picking, health & beauty, railway migration, technologies, or experience. Do NOT call this tool for scheduling, booking, calendar, or availability questions.");

        // 2. Configure Generative UI & Calendar Tools
        var downloadResumeTool = AIFunctionFactory.Create(
            DigitalTwinTools.ShowDownloadResumeCard,
            "ShowDownloadResumeCard",
            "Provides a direct download card for Ankit Sarkar's official PDF resume, LinkedIn, and GitHub links.");

        var calendarTools = new DigitalTwinCalendarTools(calComService, httpContextAccessor);

        var getSlotsTool = AIFunctionFactory.Create(
            calendarTools.GetAvailableInterviewSlots,
            "GetAvailableInterviewSlots",
            "Queries live available interview and technical screening slots on Ankit Sarkar's Cal.com calendar. Use ONLY when the user asks about open slots, free times, or scheduling availability. NEVER call this tool for questions about booked appointments, attendee names, or existing bookings (attendee data is confidential).");

        var bookInterviewTool = AIFunctionFactory.Create(
            calendarTools.BookInterviewSlot,
            "BookInterviewSlot",
            "Directly books a confirmed interview with Ankit Sarkar at the requested duration via Cal.com and dispatches a Google Meet calendar invite once attendee details are provided.");

        // 3. Compose Chat Client Pipeline:
        // Base -> OutputSanitizer -> SystemPrompt -> PromptInjectionGuard -> DailyQuota -> OpenTelemetry -> Tools -> FunctionInvocation -> Persistence
        return baseChatClient
            .AsBuilder()
            .Use((inner) => new OutputSanitizerChatClient(inner))
            .Use((inner) => new DigitalTwinSystemPromptChatClient(inner))
            .Use((inner) => new PromptInjectionGuardChatClient(inner))
            .Use((inner) => new DailyQuotaChatClient(inner, dailyQuotaService, httpContextAccessor, quotaLogger))
            .UseOpenTelemetry(
                sourceName: ResumeAssistantTelemetry.ActivitySourceName,
                configure: cfg => cfg.EnableSensitiveData = true)
            .ConfigureOptions(options =>
            {
                options.Tools ??= [];
                options.Tools.Add(searchResumeTool);
                options.Tools.Add(downloadResumeTool);
                options.Tools.Add(getSlotsTool);
                options.Tools.Add(bookInterviewTool);
            })
            .UseFunctionInvocation(configure: fic => fic.TerminateOnUnknownCalls = false)
            .Use((inner) => new DigitalTwinPersistenceChatClient(inner, historyProvider, httpContextAccessor, logger))
            .Build();
    }
}

public sealed class DigitalTwinSystemPromptChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    private static IEnumerable<ChatMessage> EnsureSystemPrompt(IEnumerable<ChatMessage> messages)
    {
        var msgList = messages.ToList();
        var systemPromptText = DigitalTwinAgentFactory.BuildSystemPrompt();

        var existingIdx = msgList.FindIndex(m => m.Role == ChatRole.System);
        if (existingIdx >= 0)
        {
            msgList[existingIdx] = new ChatMessage(ChatRole.System, systemPromptText);
        }
        else
        {
            msgList.Insert(0, new ChatMessage(ChatRole.System, systemPromptText));
        }

        return msgList;
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetResponseAsync(EnsureSystemPrompt(messages), options, cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetStreamingResponseAsync(EnsureSystemPrompt(messages), options, cancellationToken);
    }
}

