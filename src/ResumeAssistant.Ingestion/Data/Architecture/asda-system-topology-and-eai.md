---
title: "Tier-1 UK Grocery Fulfillment: System Topology & EAI Architecture"
category: "Architecture"
company: "Major UK Grocery Retailer (Tier-1 Supermarket Chain) / TCS"
role: "Principal Engineer | Technical Owner & Lead Architect"
startDate: "2023-01"
endDate: "2026-01"
sourceName: "Architecture: Tier-1 UK Grocery System Topology & EAI"
sourceLink: "#architecture-grocery-eai"
technologies:
  - "Microsoft Azure"
  - "Enterprise Application Integration (EAI)"
  - "Azure API Management (APIM)"
  - "Azure Service Bus Premium"
  - "Azure Functions (.NET 6)"
  - "Enterprise Order Management (OMS)"
  - "In-Store Picking Platform"
  - "SAP BTP"
  - "Cosmos DB"
  - "Managed Identity"
---

# Tier-1 UK Grocery Fulfillment: System Topology & EAI Architecture

The Enterprise Grocery eCommerce Fulfillment Integration Platform solves a mission-critical Enterprise Application Integration (EAI) challenge: synchronizing online grocery customer orders managed in the central **Enterprise Order Management System (OMS)** with the **In-Store Handheld Picking Application** across 600+ physical stores, while keeping downstream enterprise systems (**SAP BTP**, **Legacy Order Systems**, **Store Inventory Services**, **SharePoint printing**, and **Data Tower analytics**) eventually consistent at retail scale.

```
[Enterprise OMS / Mobile Picking Client]
       │ HTTPS + OAuth2 Bearer JWT
       ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ Tier 1: Ingress Gateway — Azure API Management (APIM)                   │
│  • validate-jwt against Entra ID OpenID metadata (role claims)          │
│  • JSON Schema validation against version-controlled JSON schemas       │
│  • Removes client subscription key; swaps for Managed Identity token    │
│  • Builds BrokerProperties (CorrelationId, SessionId, Label)            │
│  • Protocol Bridging: Direct HTTP-to-AMQP write to Service Bus REST     │
│  • Returns 202 Accepted immediately (sub-second client response)        │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ AMQP-over-HTTPS (Managed Identity)
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ Messaging Backbone: Azure Service Bus Premium                           │
│  • Partitioned Queues & Topics with Session-enabled FIFO processing     │
│  • Sharded Session IDs (StoreId_DispatchCode_random)                    │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ Session-enabled Trigger
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ Tier 2: Business Logic Compute — Azure Functions (.NET 6 CQRS)          │
│  • BaseFunction wraps MediatR commands, Result Pattern (ErrorOr)        │
│  • Strategy & Chain of Responsibility (Enrichment, Downstream Adapters) │
│  • Cosmos DB Point Reads/Upserts via System-Assigned Managed Identity   │
└─────────────────────────────────────────────────────────────────────────┘
```

## ADR-001: APIM Direct Protocol Bridging to Azure Service Bus
- **Context & Problem**: High-volume order and pick-event traffic required asynchronous ingestion without blocking client callers or incurring the cost/latency of an intermediate "gateway" Azure Function.
- **Architectural Decision**: Configured APIM inbound policies (`set-backend-service`, `rewrite-uri`, `authentication-managed-identity`) to validate incoming payloads and dispatch directly to Azure Service Bus REST API (`/messages?api-version=2015-01`), returning `202 Accepted` to the client.
- **Benefits & Trade-offs**:
  - Eliminates an entire compute tier, reducing infrastructure runtime cost and ingress latency.
  - Offloads authentication, schema validation, and correlation ID injection to the edge gateway.
  - Requires disciplined policy fragment management (`sharedoperation-policy.xml`, `schemaValidation.xml`) templated via Terraform.

## Integration Boundaries & Anti-Corruption Layer
- **Forward Flow (Order Ingestion)**: Central OMS dispatches order creation/cancellation/dispense requests $\rightarrow$ APIM validates and queues to Service Bus $\rightarrow$ Function executes CQRS command, enriches data via Chain of Responsibility (`SubstitutionsEnricher`, `LocationAndRestrictionEnricher`), invokes In-Store Picking API, and persists state in Cosmos DB.
- **Reverse Flow (Pick Events)**: In-store handheld picking webhooks emit pick events (`PickCompleted`, `Loaded`, `Dispensed`) $\rightarrow$ APIM routes by event type to Service Bus topics $\rightarrow$ Functions update Cosmos DB pick state and publish acknowledgments back to Central OMS via Kafka and SAP BTP.
- **Downstream Adapters**: External systems (SAP, Legacy Order Systems, Store Inventory Services, In-Store Picking APIs, SharePoint) are isolated behind typed `HttpClient` adapters with Polly resilience policies, preventing external schema leaks into core domain models.
