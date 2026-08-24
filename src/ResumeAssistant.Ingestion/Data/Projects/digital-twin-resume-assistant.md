---
title: "Project Showcase: Conversational Digital Twin Resume Assistant (.NET 10)"
category: "Projects"
company: "Personal Project & Open Source"
role: "Creator & Lead Architect"
startDate: "2024-01"
endDate: "Present"
sourceName: "Project: Conversational Digital Twin"
sourceLink: "#project-digital-twin"
technologies:
  - ".NET 10 / ASP.NET Core"
  - "Microsoft Agent Framework"
  - "Microsoft.Agents.AI"
  - "Microsoft.Extensions.AI"
  - "AGUI.Server"
  - "React 19 / Vite"
  - "TypeScript"
  - "CopilotKit"
  - "MongoDB Vector Search"
  - "Jina AI Embeddings"
  - "Zuplo AI Gateway"
  - "Cal.com API v2"
  - "Clerk Auth"
  - "OpenTelemetry"
  - "Grafana LGTM"
---

# Project Showcase: Conversational Digital Twin Resume Assistant

The **Conversational Digital Twin Resume Assistant** is a full-stack, enterprise-grade AI portfolio application built on **.NET 10**, **Microsoft Agent Framework**, **AG-UI Protocol**, **MongoDB Vector Search**, and **Cal.com API v2**. It reinvents the hiring experience by transforming static resumes into an authentic, interactive, real-time AI conversation with verifiable citations.

## 1. Problem Statement & Motivation
- **Static Resumes are Inefficient:** Standard 2-page PDFs cannot adequately convey the architectural depth, trade-offs, and technical nuances of 13+ years of enterprise engineering leadership.
- **AI Hallucination Risk:** Generic AI chat bots tend to invent achievements or give vague answers without verifiable proof.
- **Recruiter Friction:** Scheduling technical screening calls typically involves disjointed back-and-forth emails across calendar links.

## 2. Technical Innovations & Solution Architecture
- **Microsoft Agent Framework Decorator Pipeline:** Uses `IChatClient` chaining to compose output sanitization, system persona enforcement, prompt injection protection, session rate limiting, OpenTelemetry instrumentation, and background MongoDB persistence into a clean modular pipeline.
- **Sub-Second First-Token Streaming (AG-UI & SSE):** Implements the AG-UI server protocol adapter (`AGUI.Server`), streaming Server-Sent Events to the React frontend with dynamic markdown and citation rendering.
- **Grounded Vector RAG via MongoDB & Jina AI:** Ingests structured markdown files with YAML frontmatter, generating 1024-dimensional embeddings with Jina AI. Vector similarity queries are filtered and thresholded in MongoDB to strictly eliminate hallucinations.
- **Interactive In-Text Citation Drawers:** Clicking inline citation tags (`[1]`) immediately opens a sliding drawer with the exact source text, company, and date range.
- **Embedded Cal.com Interview Scheduling:** Real-time integration with Cal.com API v2 allows recruiters to check live calendar slots and instantly confirm interviews with Google Meet links without leaving the chat.
- **Zuplo AI Gateway with Multi-Account Pooling:** Pools multiple Cloudflare Workers AI accounts and Jina AI keys with automatic 429 failover, ensuring high availability with virtually zero cloud inference costs.
- **Recruiter Gatekeeper & Anti-Abuse:** Clerk authentication paired with real-time disposable email domain validation to ensure genuine hiring manager interactions.
- **Dual-Mode Deployment:** Runs locally with a 1-click Docker Compose environment (MongoDB, Grafana LGTM on port 3000, Inbucket) or deploys directly to production cloud infrastructure.

## 3. Key Achievements & Code Artifacts
- **Full Solution Repository:** [`ResumeAssistant.slnx`](file:///e:/Startups/resume-assistant/ResumeAssistant.slnx)
- **Backend API:** [`src/ResumeAssistant.Api`](file:///e:/Startups/resume-assistant/src/ResumeAssistant.Api)
- **Frontend SPA:** [`src/resume-assistant-frontend`](file:///e:/Startups/resume-assistant/src/resume-assistant-frontend)
- **Edge Gateway:** [`zuplo-gateway`](file:///e:/Startups/resume-assistant/zuplo-gateway)
- **Vector Ingestion CLI:** [`src/ResumeAssistant.Ingestion`](file:///e:/Startups/resume-assistant/src/ResumeAssistant.Ingestion)
