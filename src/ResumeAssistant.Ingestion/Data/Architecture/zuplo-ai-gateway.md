---
title: "Zuplo AI Gateway: Multi-Account Pooling, Dynamic Routing & Resilient Failover"
category: "Architecture"
company: "Cloud Infrastructure & Edge Engineering"
role: "Lead Cloud Architect"
startDate: "2024-01"
endDate: "Present"
sourceName: "Architecture: Zuplo AI Gateway"
sourceLink: "#architecture-zuplo-gateway"
technologies:
  - "Zuplo API Gateway"
  - "TypeScript"
  - "Edge Computing"
  - "Cloudflare Workers AI"
  - "Jina AI"
  - "Round-Robin Load Balancing"
  - "Failover & Circuit Breaking"
  - "Data Loss Prevention (DLP)"
  - "Rate Limiting"
---

# Zuplo AI Gateway: Multi-Account & Multi-Provider Pooling

The **Zuplo AI Gateway** sits at the network edge in front of the Digital Twin backend and external AI providers. It implements intelligent multi-account pooling and dynamic model routing to deliver resilient, cost-effective LLM inference and embedding generation by seamlessly distributing load across multiple accounts and automatically failing over when daily quotas or rate limits are reached.

```
                      +---------------------------------------+
                      |          ResumeAssistant.Api          |
                      |      (or any OpenAI SDK Client)       |
                      +---------------------------------------+
                                          |
                                          | POST /v1/chat/completions
                                          v
                      +---------------------------------------+
                      |           Zuplo AI Gateway            |
                      |                                       |
                      |   1. Auth & Smart DLP Guardrails      |
                      |   2. round-robin-cloudflare Policy    |
                      |   3. Dynamic Model Routing            |
                      |   4. Auto-Fallback Engine             |
                      +---------------------------------------+
                                      /       \
                         (Primary: 1st)       (Backup: 2nd)
                                    /           \
                  +----------------------+ +----------------------+
                  | Cloudflare Account 1 | | Cloudflare Account 2 |
                  |  (10,000 Neurons/Day)| |  (10,000 Neurons/Day)|
                  +----------------------+ +----------------------+
                                      |
                                      v
                        (Fails over if 429 limit hit)
```

## 1. Multi-Account Pooling & Free Tier Maximization
- Cloudflare Workers AI offers a generous free tier of **10,000 neurons per day per account**.
- Zuplo AI Gateway pools multiple Cloudflare accounts (`cloudflare-1`, `cloudflare-2`, `cloudflare-3`) into a single virtual provider.
- Requests are balanced via configurable distribution strategies (`round-robin` or `random`), effectively multiplying available inference capacity with zero infrastructure cost.

## 2. Dynamic Model Routing & Capability Detection
- The inbound policy inspects the request payload (`POST /v1/chat/completions` or `POST /v1/embeddings`).
- Automatically maps requested models (e.g. `@cf/google/gemma-4-26b-a4b-it`, `@cf/meta/llama-3.3-70b-instruct`) to capable providers in the active pool.
- Supports dedicated embedding pool routing (`round-robin-embeddings.ts`) to balance Jina AI API keys across multiple accounts.

## 3. Sub-Second Auto-Fallback & Circuit Breaking
- When an upstream provider returns an HTTP 429 (Rate Limit Exceeded) or 5xx server error, the gateway interceptor immediately routes the request to the designated backup provider in the pool.
- Eliminates end-user downtime and prevents dropped chat completions during sudden traffic spikes.
- Configurable fallback timeouts (default: 15s) ensure fast failure recovery.

## 4. Edge Security, Smart DLP & Guardrails
- **`smart-dlp.ts`:** Inspects inbound prompt payloads at the edge for sensitive PII (credit cards, social security numbers, sensitive keys) and sanitizes payloads before forwarding to external model providers.
- **Edge Rate Limiting:** Protects backend endpoints from DDoS flooding and abusive scraping by enforcing token-bucket rate limits per client IP / API key.
