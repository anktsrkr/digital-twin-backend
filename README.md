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
         │ Voyage/Jina RAG       │ LLM Chat Stream       │ OTLP Telemetry (Local/Cloud)
         ▼                       ▼                       ▼
 ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────────────────┐
 │ PostgreSQL (pgvector)│  │ Local LLM (LM Studio)│  │ Observability (Dual-Mode)        │
 │  • pgvector:pg17     │  │  • Model: lfm2.5-2.6b│  │  • Local: Grafana LGTM (port 3000│
 │  • Logto Auth (3001) │  │  • Port 1234 (/v1)   │  │  • Inbucket Email (port 9000)    │
 │  • Passwordless OTT  │  │  • Cloudflare AI     │  │  • Cloud: Grafana Cloud Free     │
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
  "Logto": {
    "Mode": "Cloud", // "Local" (Docker Compose Logto / Inbucket) or "Cloud" (Logto Cloud tenant)
    "Local": {
      "Endpoint": "http://localhost:3001",
      "AppId": "local_spa_app_id",
      "M2MAppId": "local_m2m_app_id",
      "M2MAppSecret": "local_m2m_app_secret",
      "ApiResource": "https://api.resumetwin.local",
      "MagicLinkBaseUrl": "http://localhost:5173",
      "SmtpHost": "localhost",
      "SmtpPort": 2500
    },
    "Cloud": {
      "Endpoint": "https://tenant.logto.app",
      "AppId": "YOUR_LOGTO_SPA_APP_ID",
      "M2MAppId": "YOUR_LOGTO_M2M_APP_ID",
      "M2MAppSecret": "YOUR_LOGTO_M2M_SECRET",
      "ApiResource": "https://api.resumetwin.local",
      "MagicLinkBaseUrl": "http://localhost:5173"
    }
  },
  "Supabase": {
    "Mode": "Cloud", // "Local" (Docker Compose pgvector) or "Cloud" (Hosted PostgreSQL Project)
    "Local": {
      "ConnectionString": "Host=localhost;Port=5432;Database=resume_assistant;Username=postgres;Password=postgres;SSL Mode=Disable;Trust Server Certificate=true"
    },
    "Cloud": {
      "ConnectionString": "Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.your-ref;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
    }
  }
}
```

---

## 🐳 Self-Hosted Local Docker Environment

All four infrastructure components run locally with **1 single command**:

| Container | Service | Local Port | Functionality |
| :--- | :--- | :--- | :--- |
| **`resume-postgres-vector`** | PostgreSQL 17 + `pgvector` | `5432` | Stores 1024-dim resume embeddings with HNSW cosine search & recruiter tables. |
| **`resume-logto-auth`** | Logto Identity Service | `3001` / `3002` | Handles passwordless recruiter Magic Links, one-time tokens, OIDC & JWT issuance. |
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
- 🔑 **Logto Auth Admin Console**: [`http://localhost:3001`](http://localhost:3001)
- 🐘 **PostgreSQL 17 Vector DB**: `postgresql://postgres:postgres@localhost:5432/resume_assistant`
