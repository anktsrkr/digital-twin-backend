---
title: "Zero-Trust Enterprise RAG Architecture with SpiceDB ReBAC & Vector Search"
category: "Architecture"
company: "Enterprise AI Engineering"
role: "AI Solutions Architect"
startDate: "2023-01"
endDate: "Present"
sourceName: "Architecture: Enterprise RAG with SpiceDB"
sourceLink: "#architecture-rag-spicedb"
technologies:
  - "Enterprise RAG"
  - "SpiceDB"
  - "Relationship-Based Access Control (ReBAC)"
  - "Google Zanzibar Pattern"
  - "MongoDB Vector Search"
  - "Jina AI Embeddings"
  - "Semantic Search"
  - "Zero Trust AI"
  - "Multi-Stage Ranking"
---

# Designing Zero-Trust Enterprise RAG with SpiceDB ReBAC

In enterprise and multi-tenant environments, standard Retrieval-Augmented Generation (RAG) architectures present critical data security risks. A naive vector database matches chunks purely on semantic similarity (`cosine_similarity`), leading to catastrophic prompt context injection of unauthorized documents across organizational boundaries.

I architected a **Zero-Trust RAG Platform** that integrates fine-grained **Relationship-Based Access Control (ReBAC)** powered by **SpiceDB** (Authzed / Google Zanzibar architecture) directly into the retrieval pipeline.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        ZERO-TRUST ENTERPRISE RAG ARCHITECTURE                          │
└────────────────────────────────────────────────────────────────────────────────────────┘

  User / Recruiter Query (with Auth Bearer Token)
        │
        ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │ 1. Authorization Gateway & Identity Extraction                                       │
 │  • Validates Subject ID & Tenant Context (e.g. `user:recruiter_123`)                  │
 └───────────────────────────────────────┬──────────────────────────────────────────────┘
                                         │
                                         ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │ 2. Pre-Retrieval Permission Resolution (SpiceDB / Zanzibar ReBAC)                    │
 │  • Evaluates relationships: `user:123` -> `viewer` on `document:*` or `category:*`    │
 │  • Generates Dynamic Security Predicate (Permitted Resource IDs & Filters)           │
 └───────────────────────────────────────┬──────────────────────────────────────────────┘
                                         │
                                         ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │ 3. Constrained Vector Similarity Search (MongoDB Vector Store)                       │
 │  • Jina AI Embeddings (`jina-embeddings-v3` 1024-dim dense vectors)                  │
 │  • Vector search executes ONLY across permitted partition space                      │
 └───────────────────────────────────────┬──────────────────────────────────────────────┘
                                         │
                                         ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │ 4. Multi-Stage Scoring & Reranking                                                   │
 │  • Secondary Cross-Encoder Reranking for high contextual precision                   │
 │  • Verified citations anchored with immutable source provenance IDs                  │
 └───────────────────────────────────────┬──────────────────────────────────────────────┘
                                         │
                                         ▼
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │ 5. LLM Prompt Context Generation (Strict Zero-Leakage Guarantee)                     │
 └──────────────────────────────────────────────────────────────────────────────────────┘
```

## 1. The Security Dilemma in Multi-Tenant Enterprise RAG
- In enterprise systems, knowledge chunks belong to different access tiers (e.g. public, recruiter-accessible, internal executive, sensitive financials).
- Traditional RAG queries fetch the top-$k$ nearest neighbors globally. If an external or unauthorized user crafts a semantically similar prompt, sensitive data is retrieved into the LLM context, violating data governance and compliance (GDPR/SOC2).

## 2. Pre-Retrieval Authorization & Dynamic Predicates via SpiceDB (Google Zanzibar ReBAC)
- Defined Zanzibar-style relationship schemas in SpiceDB:
  ```
  definition user {}
  definition organization {
      relation member: user
  }
  definition document {
      relation viewer: user
      relation org: organization
      permission read = viewer + org->member
  }
  ```
- Before vector search executes, the pipeline checks SpiceDB to compute the caller's authorized document ID set.
- Vector search in MongoDB is constrained using metadata filtering (`{ "resourceId": { "$in": authorizedIds } }`), ensuring the search space is strictly bounded.

## 3. High-Density Vector Ingestion & Multi-Stage Scoring
- Ingestion pipelines split documents along semantic markdown boundaries, generating dense embeddings with **Jina AI (`jina-embeddings-v3`)**.
- Vector results undergo two-stage scoring (Vector Cosine Distance $\rightarrow$ Cross-Encoder Reranking) to eliminate false-positive semantic matches.

## 4. Zero-Leakage Guarantees & Immutable Citation Audit Provenance
- Every passage injected into the context window carries an immutable citation tracking hash (`sourceName`, `sourceLink`, `chunkId`).
- Guarantees zero cross-tenant hallucination and provides complete audit traceability for enterprise compliance.
