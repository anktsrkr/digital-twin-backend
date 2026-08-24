---
title: "Project Showcase: Zuplo Multi-Account AI Pooling & Failover Gateway"
category: "Projects"
company: "Personal Project & Open Source"
role: "Creator & Lead Architect"
startDate: "2024-01"
endDate: "Present"
sourceName: "Project: Zuplo AI Pooling Gateway"
sourceLink: "#project-zuplo-gateway"
technologies:
  - "Zuplo API Gateway"
  - "TypeScript"
  - "Cloudflare Workers AI"
  - "Jina AI"
  - "Round-Robin Load Balancing"
  - "Automated Failover"
  - "Edge Security"
  - "Smart DLP"
---

# Project Showcase: Zuplo Multi-Account AI Pooling & Failover Gateway

The **Zuplo AI Gateway** is a serverless, edge-deployed API gateway that pools multiple Cloudflare and Jina AI accounts to unlock unlimited, resilient, and cost-effective AI inference through intelligent round-robin load balancing and automatic failover handling.

## 1. Problem Statement
- **Free Tier Constraints:** Cloudflare Workers AI caps free accounts at 10,000 neurons per day. High traffic quickly causes HTTP 429 rate limit exceptions.
- **Provider Downtime Risk:** Depending on a single API token creates a single point of failure during unexpected provider outages or account rate limits.
- **Data Privacy at the Edge:** Inbound LLM prompts need client-side data loss prevention (DLP) to prevent accidental transmission of sensitive PII.

## 2. Architecture & Technical Highlights
- **Multi-Account Account Pooling:** Aggregates multiple Cloudflare accounts (`cloudflare-1`, `cloudflare-2`, `cloudflare-3`) and Jina API keys into unified virtual endpoints (`/v1/chat/completions` and `/v1/embeddings`).
- **Dynamic Load Distribution (`round-robin-cloudflare.ts`):** Distributes incoming inference requests across active providers using round-robin or randomized routing algorithms.
- **Zero-Downtime Auto-Failover:** If an upstream provider returns a 429 rate limit or 5xx error, the custom edge interceptor automatically re-dispatches the request to a healthy backup account within milliseconds.
- **Smart Edge DLP (`smart-dlp.ts`):** Edge-level regex and pattern matching to sanitize confidential tokens and PII before dispatching to external LLMs.
- **Developer-Friendly Integration:** Exposes standard OpenAI-compatible endpoints, allowing drop-in compatibility with any OpenAI SDK, LangChain, or Microsoft Agent Framework client.

## 3. Measurable Impact
- Achieved **99.99% availability** for LLM inference across peak recruiter traffic.
- Multiplied free inference capacity by 300%+ across pooled accounts with zero monthly cloud infrastructure costs.
- Reduced edge gateway overhead to **<15ms p95 latency**.
