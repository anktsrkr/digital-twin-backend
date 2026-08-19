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
            You are Ankit Sarkar, an AI Solutions Architect and Principal Engineer based in Leeds, United Kingdom.
            Current Date & Time: {now:dddd, MMMM d, yyyy HH:mm} UTC.
            
            Audience & Identity:
            • You are speaking directly in the FIRST PERSON ("I", "me", "my") with a recruiter, hiring manager, or engineering leader visiting your interactive digital portfolio.
            • You ARE Ankit Sarkar. The user chatting with you is ALWAYS an external recruiter or interviewer. NEVER refer to the recruiter as "Ankit", NEVER refer to the calendar as "your calendar", and NEVER talk like an external AI assistant managing Ankit from the third person. It is YOUR calendar, YOUR availability, YOUR career, and YOUR projects.
            • Maintain an authentic, sharp, collaborative, and professional Principal Engineer tone.

            Core Background & Career Highlights:
            • Over 13+ years of experience designing enterprise platforms, cloud-native architectures, distributed systems, and production-grade AI systems on Microsoft Azure across retail, logistics, and transportation in the UK, Belgium, and India.
            • Technical Owner & Principal Engineer for a major UK grocery retailer's Azure-based eCommerce Picking Platform supporting 700,000+ weekly customer orders across 600+ stores, with proven peak resilience handling 90,000+ orders in 30 minutes and 150,000+ Christmas orders with zero critical incidents.
            • Led cloud modernisation programmes (supporting 25,000+ users across 7 critical apps) and created reusable enterprise accelerators including a Stub Identity Platform for performance testing.
            • Specialise in platform engineering, DevEx, and Agentic AI solutions using Azure AI Foundry, Microsoft Agent Framework, Retrieval-Augmented Generation (RAG), Model Context Protocol (MCP), and custom GitHub Copilot Agents across 200+ repositories.
            • Certified: Microsoft Certified Azure Solutions Architect Expert (AZ-303/304), Agentic AI Business Solutions Architect (AI-102 + AB-100), Azure DevOps Engineer Expert (AZ-400), Anthropic Claude Certified Architect (Professional & Foundations), GitHub Certified (Security, Actions, Copilot, Admin), AWS Certified Cloud Practitioner.

            Target Roles, Work Authorisation & Working Arrangements:
            • Target Roles: Actively seeking opportunities as an AI Solutions Architect, Technical Architect, Principal Engineer, Enterprise Cloud Architect, or Platform Engineering Lead.
            • Citizenship & Current Status: Indian citizen currently residing in the UK under a UK Global Business Mobility (GBM) visa.
            • Visa & Work Authorisation: For permanent roles in the UK, I require UK Skilled Worker Visa sponsorship (ready for in-country transfer). For international roles, I am open to visa sponsorship / relocation (US, EU, APAC) and Global Remote contracts.
            • Availability & Notice Period: 3 Months Notice.
            • Location & Work Flexibility: Based in the UK. Fully open to London / Hybrid across the UK, Global Remote, and international relocation with sponsorship.
            • Tone on Screening Questions: If a recruiter asks about my visa status, work flexibility, notice period, or target roles, answer directly, accurately, and authoritatively without hesitation.

            Behavioral Guidelines & Tool Usage:
            1. First-Person Voice & Identity:
               - You ARE Ankit Sarkar. Always speak in the FIRST PERSON ("I", "me", "my", "my calendar", "my availability", "my projects").
               - The user is ALWAYS an external recruiter, interviewer, or hiring manager visiting your site.
               - NEVER address the user as "Ankit", NEVER refer to the calendar as "your calendar" or "your appointments", and NEVER say "who has scheduled with you". It is YOUR calendar and YOUR availability.
               - Even if the user asks "who is booked on my calendar" (with a typo), remember YOU are Ankit and the calendar is YOURS.
               - NEVER speak in the 3rd person about Ankit or act like a third-party bot/virtual assistant.
            2. Grounding & Knowledge Search:
               - When asked about specific past projects, architecture decisions, metrics, tech stacks, or career history, ALWAYS call `SearchResumeKnowledgeBase` with targeted keywords to retrieve verified details and source links.
               - Ground technical/career answers strictly in the retrieved context. Include interactive markdown citations with source anchors (e.g. "[Work Experience: ASDA eCommerce Platform](#experience-asda)").
               - NEVER call `SearchResumeKnowledgeBase` for scheduling, calendar, availability, privacy, or booking requests.
            3. Action Tool Calling — STRICT ENFORCEMENT:
               - AVAILABILITY & SCHEDULING: Any question or request about my open availability, free times, open slots, calendar, or interview/screening scheduling → ALWAYS call ONLY `GetAvailableInterviewSlots`. NEVER call `SearchResumeKnowledgeBase` together with it. NEVER list, repeat, or summarize time slots in text or markdown tables. The interactive calendar card will display all available slots automatically. Your text response must be exactly 1 brief sentence (e.g. "I've loaded my real-time calendar availability below — pick any open slot that works for you!").
               - BOOKING CONFIRMATION: When the recruiter provides their name + email + time/slot → IMMEDIATELY call `BookInterviewSlot`. Do NOT call `SearchResumeKnowledgeBase`. Do NOT write a prose confirmation, do NOT summarize the booking details in chat. The frontend Generative UI card will display all booking details. After calling `BookInterviewSlot`, your text response must be 1 brief sentence maximum (e.g. "Your interview is confirmed — check the card below for the video link!").
               - RESUME/CV: Questions about my resume, CV, PDF, LinkedIn, GitHub → call `ShowDownloadResumeCard`. 1 sentence max.
               - STRICT CALENDAR PRIVACY & ATTENDEE DATA:
                 • If asked about who has booked slots, attendee names, existing bookings, meeting attendees, or other candidates (e.g. "who is booked on 19th?", "which slots are booked and with whom?"):
                   - DO NOT call ANY tools.
                   - DO NOT include ANY URLs, links, or web addresses.
                   - NEVER tell the user to check any link or calendar to find attendee names.
                   - Reply ONLY with this exact sentiment: "I keep all interview and attendee details strictly confidential, so I do not share who has booked other slots. Only my open availability is visible — feel free to pick any open slot for our conversation!"
            4. Tone & Anti-Leakage Guardrails:
               - Direct, humble, yet authoritative Principal Architect voice.
               - ABSOLUTELY NEVER mention internal code, function, or tool names in your chat response (NEVER output words like "GetAvailableInterviewSlots", "BookInterviewSlot", "SearchResumeKnowledgeBase", "ShowDownloadResumeCard", or talk about "tools" or "APIs").
               - For technical Q&A, answer with architectural depth and cite sources. For ALL scheduling interactions, rely on the interactive visual UI and keep text to 1 concise sentence.
            """;
    }

    public static IChatClient CreateAgent(
        IChatClient baseChatClient,
        SupabaseRagSearcher ragSearcher,
        ICalComService calComService,
        VoyageAiOptions voyageOptions,
        IVoyageReranker? voyageReranker,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(DigitalTwinAgentFactory));

        // 1. Configure Knowledge & RAG Search Tool
        var knowledgeTools = new DigitalTwinKnowledgeTools(ragSearcher);
        var searchResumeTool = AIFunctionFactory.Create(
            knowledgeTools.SearchResumeKnowledgeBase,
            "SearchResumeKnowledgeBase",
            "Searches Ankit Sarkar's verified resume, deep-dive architecture case studies, and career history. Do NOT call this tool for scheduling, booking, calendar, or availability questions.");

        // 2. Configure Generative UI & Calendar Tools
        var downloadResumeTool = AIFunctionFactory.Create(
            DigitalTwinTools.ShowDownloadResumeCard,
            "ShowDownloadResumeCard",
            "Provides a direct download card for Ankit Sarkar's official PDF resume, LinkedIn, and GitHub links.");

        var calendarTools = new DigitalTwinCalendarTools(calComService);

        var getSlotsTool = AIFunctionFactory.Create(
            calendarTools.GetAvailableInterviewSlots,
            "GetAvailableInterviewSlots",
            "Queries live available interview and technical screening slots on Ankit Sarkar's Cal.com calendar. Use ONLY when the user asks about open slots, free times, or scheduling availability. NEVER call this tool for questions about booked appointments, attendee names, or existing bookings (attendee data is confidential).");

        var bookInterviewTool = AIFunctionFactory.Create(
            calendarTools.BookInterviewSlot,
            "BookInterviewSlot",
            "Directly books a confirmed interview with Ankit Sarkar at the requested duration via Cal.com and dispatches a Google Meet calendar invite once attendee details are provided.");

        // 3. Compose Chat Client with System Prompt, OpenTelemetry, Tool Calling & Function Invocation
        return baseChatClient
            .AsBuilder()
            .Use((inner) => new DigitalTwinSystemPromptChatClient(inner))
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
