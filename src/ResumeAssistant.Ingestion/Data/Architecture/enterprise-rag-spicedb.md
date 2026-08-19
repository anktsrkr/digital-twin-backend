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
  - "Supabase pgvector"
  - "Voyage AI"
  - "Jina AI"
  - "Semantic Search"
  - "Zero Trust AI"
  - "Vector Databases"
---

# Designing Zero-Trust RAG with SpiceDB ReBAC & Dense Vector Embeddings

In enterprise environments, standard Retrieval-Augmented Generation (RAG) poses significant data leakage risks when querying multi-tenant documents across diverse roles and clearance levels. I architected a Zero-Trust RAG platform integrating fine-grained Relationship-Based Access Control (ReBAC) using **SpiceDB** (AuthZed / Google Zanzibar pattern) with high-density vector retrieval.

## The Security Problem in Enterprise RAG
Naive vector retrieval matches documents strictly based on semantic similarity (`cosine_distance`). If an unauthorized user asks a question about confidential executive summaries or partner pricing, a standard vector search will retrieve and inject those sensitive passages into the prompt context.

## Architectural Implementation

### 1. Pre-Retrieval Permission Checking via SpiceDB
- Defined Zanzibar-style relationship schemas in SpiceDB (`user:recruiter -> member_of -> org:retailer`).
- Queries pass through an authorization middleware before vector search executes, filtering the candidate vector space strictly to documents the caller has explicit `view` permissions for.

### 2. Dense Vector Ingestion & Multi-Stage Ranking
- Documents are partitioned semantically and embedded using **Voyage AI (`voyage-3-lite`)** and **Jina AI (`jina-embeddings-v3`)** into **PostgreSQL / Supabase `pgvector`**.
- Candidate vector results undergo secondary reranking via **Voyage Reranker** for maximum precision before LLM context injection.

### 3. Traceability & Zero-Leakage Guarantee
- Enforces an immutable audit log for every chunk retrieved and cited by the agent.
- Prevents cross-tenant hallucination and unauthorized data exposure at the database layer.
