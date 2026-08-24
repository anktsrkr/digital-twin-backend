---
title: "Tier-1 UK Retail Architecture: Cosmos DB Tiered Data Strategy & RU Optimization"
category: "Architecture"
company: "Major UK Grocery Retailer (Tier-1 Supermarket Chain) / TCS"
role: "Principal Engineer | Technical Owner & Lead Architect"
startDate: "2023-01"
endDate: "2026-01"
sourceName: "Architecture: Cosmos DB Data Strategy & RU Optimization"
sourceLink: "#architecture-cosmosdb-strategy"
technologies:
  - "Azure Cosmos DB (SQL/Core API)"
  - "RU/s Cost Optimization"
  - "Indexing Policy Design"
  - "Bounded Staleness Consistency"
  - "Azure Synapse Link"
  - "Managed Identity RBAC"
---

# Azure Cosmos DB Tiered Data Strategy & High-Throughput Optimization

In the Enterprise Grocery eCommerce Fulfillment Integration Platform, **Azure Cosmos DB** serves as the primary system of record for order creation state and in-store pick event reconciliation. The database platform is engineered to sustain high-volume write bursts during weekly slot releases and peak promotional periods while guaranteeing sub-10ms point read/upsert latencies and strict RU cost containment.

```
┌────────────────────────────────────────────────────────────────────────┐
│               COSMOS DB "PICKING" DATABASE ARCHITECTURE                │
├────────────────────────────────┬───────────────────────────────────────┤
│ Container: OrderCreate         │ Container: PickEvents                 │
│  • Partition Key: /id          │  • Partition Key: /id                 │
│  • Unique Key: /orderid        │  • Unique Key: /orderid               │
│  • Operational TTL: 14 Days    │  • Operational TTL: 14 Days           │
│  • Autoscale Max: 48,000 RU/s  │  • Autoscale Max: 12,000 RU/s         │
│  • Indexing: Exclude "/*"      │  • Indexing: Exclude "/*"             │
│    Include "/orderid/?" only   │    Include "/orderid/?" only          │
└────────────────────────────────┴───────────────────────────────────────┘
                                 │
                 Synapse Link (Analytical Store)
                                 ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Enterprise Analytics / Data Tower (Zero Transactional RU Impact)       │
└────────────────────────────────────────────────────────────────────────┘
```

## 1. ADR-004: Radical RU Reduction via Selective Indexing (/* Excluded)
Standard Cosmos DB indexing indexes every field in a JSON document (`/*`), consuming substantial Request Units (RUs) on every write and upsert operation:
- **Architectural Decision**: Configured explicit indexing policies in Terraform:
  - Excluded all document paths: `excluded_path = ["/*"]`
  - Included only the lookup path: `included_path = ["/orderid/?"]`
- **Cost Impact**: Slashed write Request Unit (RU) consumption by **>60%**, allowing the platform to absorb intense traffic surges without throttling or unnecessary capacity over-provisioning.

## 2. High-Performance Point-Read Data Access Layer (ICosmosService)
- Application code intentionally avoids expensive SQL and LINQ cross-partition queries.
- All operations execute as high-performance point operations via `ReadItemAsync(orderId, PartitionKey(orderId))` and `UpsertItemAsync(document, PartitionKey(orderId))`.
- The custom `ICosmosService` abstraction wraps Cosmos SDK exceptions into typed domain failures (`Error.Failure("COSMOS_ISSUE", ex.Message)`), ensuring clean failure propagation through the MediatR and Railway-Oriented pipeline.

## 3. Consistency Level Strategy: Bounded Staleness
- Selected **Bounded Staleness** (configured to 300 seconds / 100,000 operations) rather than *Strong Consistency*.
- **Architectural Rationale**: Delivers predictable, low-latency writes across availability zones while providing mathematically bounded read staleness, ensuring subsequent `Loaded` and `Dispensed` handlers reliably observe the just-written `PickCompleted` record.

## 4. Zero-Cost Analytics Isolation via Azure Synapse Link
- Enabled Cosmos DB Analytical Store with independent analytical TTL on transactional containers.
- Enterprise BI analytics, audit reports, and historical reconciliation queries execute against the analytical columnar store without competing for transactional Request Units (RUs) or impacting live grocery picking operations.
