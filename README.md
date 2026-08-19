# Conversational Digital Twin Resume Assistant (.NET 10)

An interactive, real-time AI "Digital Twin" of your professional resume. Instead of static 2-page PDFs, recruiters engage in an authentic, real-time conversational chat, receiving instant, factual, and cited answers regarding your experience, system architectures, and engineering achievements.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                 SYSTEM ARCHITECTURE                                    │
└────────────────────────────────────────────────────────────────────────────────────────┘

  Recruiter Browser (Desktop / Mobile)
       │
       ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │ Frontend (Vite + React + CopilotKit / AG-UI Client)                                  │
 │  • Design: Modern AI-Native Light Theme (Crisp, generous spacing, clean typography)  │
 │  • Recruiter Gate: Supabase Magic Link Login + Disposable Email Blocker              │
 │  • Rich Markdown Rendering: react-markdown + remark-gfm + Interactive Citations      │
 │  • Real-time SSE Chat Stream with Interactive Citation Drawers                       │
 │  • Generative UI Cards (Book Meeting / Download PDF / Skill Deep-Dive)               │
 └───────────────────────────────────────┬──────────────────────────────────────────────┘
                                         │ HTTPS / SSE (Bearer Supabase JWT)
                                         ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │ Backend (.NET 10 ASP.NET Core Web API)                                               │
 │  • Hosting: Docker / SnapDeploy / Out Plane / Caasify                                │
 │  • AG-UI Server Protocol Adapter (AGUI.Server + Microsoft.Agents.AI)                 │
 │  • Dual-Mode Architecture (1-Click Local Docker vs Production Cloud)                 │
 │  • Persona Prompt Engine (First-person "Digital Twin", strict anti-hallucination)    │
 └───────┬───────────────────────┬───────────────────────┬──────────────────────────────┘
         │                       │                       │
         │ Voyage AI RAG         │ LLM Chat Stream       │ OTLP Telemetry (Local/Cloud)
         ▼                       ▼                       ▼
 ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────────────────┐
 │ Supabase (Dual-Mode) │  │ Local LLM (LM Studio)│  │ Observability (Dual-Mode)        │
 │  • Mode: Local/Cloud │  │  • Model: lfm2.5-2.6b│  │  • Local: Grafana LGTM (port 3000│
 │  • pgvector:pg17     │  │  • Port 1234 (/v1)   │  │  • Inbucket Email (port 9000)    │
 │  • GoTrue Auth (9999)│  │  • Cloudflare AI     │  │  • Cloud: Grafana Cloud Free     │
 └──────────────────────┘  └──────────────────────┘  └──────────────────────────────────┘
```

---

## ⚙️ Configuration (Local vs Cloud Modes)

In `src/ResumeAssistant.Api/appsettings.json`:

```json
{
  "Telemetry": {
    "Mode": "Local", // "Local" (Docker Compose Grafana LGTM) or "Cloud" (Grafana Cloud Free Tier) or "None"
    "Local": {
      "OtlpEndpoint": "http://localhost:4318"
    },
    "Cloud": {
      "OtlpEndpoint": "https://otlp-gateway-prod-eu-west-0.grafana.net/otlp",
      "InstanceId": "YOUR_GRAFANA_INSTANCE_ID",
      "ApiToken": "YOUR_GRAFANA_API_TOKEN"
    }
  },
  "Supabase": {
    "Mode": "Local", // "Local" (Docker Compose pgvector + GoTrue) or "Cloud" (Hosted Supabase Project)
    "Local": {
      "Url": "http://localhost:9999",
      "AnonKey": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.local-anon-token",
      "ConnectionString": "Host=localhost;Port=5432;Database=resume_assistant;Username=postgres;Password=postgres;SSL Mode=Disable;Trust Server Certificate=true"
    },
    "Cloud": {
      "Url": "https://your-project.supabase.co",
      "AnonKey": "your-cloud-anon-key",
      "ConnectionString": "Host=aws-0-us-east-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.your-ref;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
    }
  },
  "LocalLLM": {
    "Enabled": true,
    "Endpoint": "http://localhost:1234/v1",
    "Model": "lfm2.5-2.6b",
    "ApiKey": "lm-studio"
  }
}
```

---

## 🐳 Self-Hosted Local Docker Environment

All four infrastructure components run locally with **1 single command**:

| Container | Service | Local Port | Functionality |
| :--- | :--- | :--- | :--- |
| **`resume-postgres-vector`** | PostgreSQL 17 + `pgvector` | `5432` | Stores 1024-dim resume embeddings with HNSW cosine search & recruiter tables. |
| **`resume-supabase-auth`** | Supabase GoTrue Auth | `9999` | Handles passwordless recruiter Magic Links, OTPs, and JWT token issuance. |
| **`resume-inbucket`** | Inbucket Email Testing UI | `9000` | Catches local Magic Link emails in the browser so you can log in without real SMTP. |
| **`resume-grafana-lgtm`** | Grafana LGTM OpenTelemetry | `3000` | Live traces (Tempo), logs (Loki), metrics (Mimir/Prometheus), and OTLP ingestion. |

---

## 🚀 Quick Start Guide

### Step 1: Start the Docker Infrastructure
```bash
docker compose up -d
```

### Step 2: Start LM Studio
1. Open **LM Studio** and load your model (e.g. `lfm2.5-2.6b`).
2. Start the Local Server on port `1234` (`http://localhost:1234/v1`).

### Step 3: Run the .NET 10 API
```bash
dotnet run --project src/ResumeAssistant.Api/ResumeAssistant.Api.csproj
```
The API starts on `http://localhost:5000` with the AG-UI streaming endpoint at `POST /agentic_chat`.

### Step 4: Run the Frontend
```bash
cd src/resume-assistant-frontend
npm run dev
```

---

## 🌐 Local Developer Port Directory

- 💬 **Digital Twin Chat UI**: [`http://localhost:5173`](http://localhost:5173) (or `5174`)
- ✉️ **Inbucket Email Inbox (View Magic Links)**: [`http://localhost:9000`](http://localhost:9000)
- 📊 **Grafana Observability Dashboard**: [`http://localhost:3000`](http://localhost:3000) *(User: `admin` / Password: `admin`)*
- 🔑 **Supabase GoTrue Auth API**: [`http://localhost:9999`](http://localhost:9999)
- 🐘 **PostgreSQL 17 Vector DB**: `postgresql://postgres:postgres@localhost:5432/resume_assistant`
