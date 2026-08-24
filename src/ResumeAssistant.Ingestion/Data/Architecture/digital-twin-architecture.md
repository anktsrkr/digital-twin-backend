---
title: "Conversational Digital Twin Architecture: Microsoft Agent Framework, .NET 10 & MongoDB RAG"
category: "Architecture"
company: "Enterprise AI Engineering & Open Source"
role: "AI Solutions Architect & Lead Engineer"
startDate: "2024-01"
endDate: "Present"
sourceName: "Architecture: Digital Twin Platform"
sourceLink: "#architecture-digital-twin"
technologies:
  - "Microsoft Agent Framework"
  - "Microsoft.Agents.AI"
  - "Microsoft.Extensions.AI"
  - "AGUI.Server"
  - ".NET 10 / C#"
  - "MongoDB Vector Search"
  - "Jina AI Embeddings"
  - "Zuplo AI Gateway"
  - "Cal.com API v2"
  - "Clerk Auth"
  - "OpenTelemetry"
  - "Grafana LGTM"
  - "Docker Compose"
---

# Architecture Deep Dive: Conversational Digital Twin (.NET 10)

The **Conversational Digital Twin Resume Assistant** is a real-time, interactive AI portfolio platform built with **.NET 10**, **Microsoft Agent Framework** (`Microsoft.Agents.AI` and `Microsoft.Extensions.AI`), **AG-UI Server Protocol**, **MongoDB Vector Search**, and **Cal.com API v2**. It converts static CVs into an interactive, factual, and cited conversational experience for recruiters and engineering leaders.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        DIGITAL TWIN SYSTEM ARCHITECTURE                                │
└────────────────────────────────────────────────────────────────────────────────────────┘

  Recruiter Browser (Desktop / Mobile)
       │
       ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │ Frontend (Vite + React 19 + CopilotKit / AG-UI Client)                               │
 │  • Real-time SSE Chat Stream with Interactive Citation Drawers                       │
 │  • Generative UI Cards (Live Cal.com Slot Picker, PDF Download, Dossier)             │
 │  • Recruiter Gate: Clerk Auth + Disposable Email Domain Blocker                      │
 └───────────────────────────────────────┬──────────────────────────────────────────────┘
                                         │ HTTPS / SSE (Bearer JWT)
                                         ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │ Backend (.NET 10 ASP.NET Core API - Microsoft.Agents.AI)                             │
 │  • AG-UI Server Protocol Endpoint: POST /agentic_chat                                │
 │  • Decorator Agent Pipeline (IChatClient):                                           │
 │    OutputSanitizer -> SystemPrompt -> InjectionGuard -> DailyQuota -> OTel -> Tools  │
 │    -> FunctionInvocation -> DigitalTwinPersistence                                   │
 └───────┬───────────────────────┬───────────────────────┬──────────────────────────────┘
         │                       │                       │
         │ Jina Vector Search    │ LLM Inference via     │ OTLP Telemetry (Tempo/Loki/Mimir)
         ▼                       ▼ Zuplo AI Gateway      ▼
 ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────────────────┐
 │ MongoDB Vector Store │  │ Cloudflare Workers AI│  │ Grafana LGTM Observability       │
 │  • resume_assistant  │  │  • Multi-Account Pool│  │  • Traces (Tempo APM)            │
 │  • HNSW Cosine Index │  │  • Gemma / Llama     │  │  • Metrics (Mimir / Prometheus)  │
 │  • Chat History      │  │  • Auto-Failover     │  │  • Logs (Loki OpenTelemetry)     │
 └──────────────────────┘  └──────────────────────┘  └──────────────────────────────────┘
```

## 1. Microsoft Agent Framework & Decorator Pipeline
The core chat orchestration leverages Microsoft's modern AI standard abstractions (`Microsoft.Extensions.AI` and `Microsoft.Agents.AI`). The agent pipeline is structured using a robust **Decorator / DelegatingChatClient Pipeline**:

```csharp
// Agent Pipeline Composition
return baseChatClient
    .AsBuilder()
    .Use((inner) => new OutputSanitizerChatClient(inner))
    .Use((inner) => new DigitalTwinSystemPromptChatClient(inner))
    .Use((inner) => new PromptInjectionGuardChatClient(inner))
    .Use((inner) => new DailyQuotaChatClient(inner, dailyQuotaService, httpContextAccessor, quotaLogger))
    .UseOpenTelemetry(sourceName: "ResumeAssistant.Api", cfg => cfg.EnableSensitiveData = true)
    .ConfigureOptions(options => {
        options.Tools.Add(searchResumeTool);
        options.Tools.Add(downloadResumeTool);
        options.Tools.Add(getSlotsTool);
        options.Tools.Add(bookInterviewTool);
    })
    .UseFunctionInvocation(configure: fic => fic.TerminateOnUnknownCalls = false)
    .Use((inner) => new DigitalTwinPersistenceChatClient(inner, historyProvider, httpContextAccessor, logger))
    .Build();
```

- **`OutputSanitizerChatClient`:** Strips internal model reasoning tags (`<think>`, `<reasoning>`), removes leaked internal function signatures, and standardizes output for recruiter presentation.
- **`DigitalTwinSystemPromptChatClient`:** Injects the authoritative first-person persona prompt with real-time UTC timestamping, strict anti-hallucination rules, and bounding controls.
- **`PromptInjectionGuardChatClient`:** Detects and neutralizes jailbreak attempts, delimiter manipulation, and prompt extraction attacks.
- **`DailyQuotaChatClient`:** Enforces daily message rate limits and quotas per recruiter session, backed by MongoDB.
- **`DigitalTwinPersistenceChatClient`:** Asynchronously writes chat turns, citations, and conversation metadata to MongoDB for auditing and session continuity.

## 2. AG-UI Protocol & Streaming Execution
- Integrated `AGUI.Server` via `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` to expose a standardized streaming endpoint (`POST /agentic_chat`).
- Streams Server-Sent Events (SSE) directly to the React frontend, delivering sub-second First-Token Latency (TTFT) and dynamic client-side rendering.

## 3. High-Precision Vector RAG with MongoDB & Jina AI
- **Embedding Generation:** Ingestion pipeline converts Markdown files with YAML frontmatter into semantic chunks, generating 1024-dimensional embeddings via **Jina AI (`jina-embeddings-v3`)**.
- **MongoDB Vector Search (`MongoDbRagSearcher`):** Performs vector similarity search with HNSW indexing on the `resume_assistant` database, enforcing score thresholds to prevent irrelevant context injection.
- **Strict Grounding:** The agent is mandated to invoke `SearchResumeKnowledgeBase` for any project, metric, or technical architecture inquiry, returning verified citations with source anchors.

## 4. Live Interview Scheduling with Cal.com API v2
- **`GetAvailableInterviewSlots`:** Dynamically queries live availability on Ankit's calendar across selectable meeting formats (15-min intro, 30-min screening, 60-min system design).
- **`BookInterviewSlot`:** Automatically reserves confirmed interview slots via Cal.com API v2 and dispatches Google Meet invites to both parties without external context switching.

## 5. Recruiter Gatekeeper & Anti-Abuse
- **Clerk Authentication:** Manages recruiter sign-in and token issuance.
- **`DisposableEmailValidator`:** Validates recruiter email domains against known temporary/disposable email providers, ensuring genuine hiring manager engagements.

## 6. End-to-End Observability with Grafana LGTM
- Fully instrumented with **OpenTelemetry (OTel)** for distributed tracing, runtime metrics, and logs.
- Dual-mode architecture supports 1-click local Docker deployment (`resume-grafana-lgtm` on port 3000) or direct cloud export to Grafana Cloud via OTLP over HTTP/Protobuf.
