---
title: "Deep Dive: Architecting ASDA's High-Scale Azure eCommerce Picking Platform"
category: "Architecture"
company: "ASDA / Tata Consultancy Services"
role: "Principal Engineer | Technical Owner & Lead Architect"
startDate: "2023-01"
endDate: "Present"
sourceName: "Architecture Deep Dive: ASDA Picking Platform"
sourceLink: "#architecture-asda-picking"
technologies:
  - "Microsoft Azure"
  - "Event-Driven Architecture (EDA)"
  - "Azure Service Bus (Premium)"
  - "Azure Event Grid"
  - "Azure Event Hubs"
  - "Azure Functions (Isolated Worker)"
  - "Durable Functions / Durable Task"
  - "Azure Cosmos DB"
  - "Redis Cache"
  - "Terraform"
  - "Azure Monitor & App Insights"
  - "DevSecOps"
---

# Designing ASDA's Enterprise eCommerce Grocery Picking Platform

As the Technical Owner and Lead Architect, I led the architectural design and engineering implementation of the nationwide Azure-based eCommerce picking platform for ASDA, one of the UK's largest grocery retailers. 

The platform is responsible for orchestrating in-store and warehouse picking for **over 700,000 weekly online grocery orders across 600+ physical stores**.

## The Architectural Challenge
Online grocery fulfilment presents unique high-concurrency challenges:
- **Massive Flash Surges:** Flash promotions and delivery slot releases generate demand bursts of **90,000+ customer orders within 30 minutes**.
- **Peak Seasonal Volumes:** Managing over **150,000+ Christmas orders** without platform degradation.
- **Physical Store Constraints:** 600+ stores with thousands of handheld picking devices requiring real-time item routing, substitutions, and weight adjustments.
- **Zero-Loss Requirement:** Inability to drop an order even under extreme downstream system downtime.

## Core Architectural Pillars

### 1. Decoupled Event-Driven Ingestion with Azure Service Bus & Event Grid
- Implemented asynchronous event-driven ingestion using **Azure Service Bus Premium** sessions and partitioned queues.
- Orders are ingested from the digital eCommerce storefront as immutable domain events via **Azure Event Grid** and queued into dedicated picking queues.
- Decoupling order ingestion from store picking execution guarantees that checkout operations remain 100% available even during intermittent in-store network outages.

### 2. Scalable Orchestration with Azure Functions & Durable Task
- Utilised **Azure Functions .NET Isolated Worker** with auto-scaling consumption and premium plans.
- Leveraged the **Durable Task Framework / Durable Entities** to model picking sessions, stateful tote management, and multi-zone store picking batches.
- Sub-divided complex multi-temperature orders (ambient, chilled, frozen) into concurrent picking waves coordinated by orchestrators.

### 3. Data Tier & Caching: Cosmos DB & Redis
- High-throughput store picking state cached in **Azure Cache for Redis** for sub-millisecond handheld scanner responses.
- Long-term order persistence, item substitution history, and audit trails persisted in **Azure Cosmos DB** with partition keys optimized on `StoreId` and `OrderId`.

### 4. Idempotency & Resiliency Engineering
- Strict message deduplication and idempotent consumer patterns to eliminate duplicate picks or charge errors.
- Dead Letter Queue (DLQ) automated replay pipelines with exponential backoff and circuit-breaker telemetry.
- Comprehensive end-to-end distributed tracing across microservices using **W3C TraceContext**, Azure Application Insights, and custom Log Analytics workbooks.

### 5. Infrastructure as Code & InnerSource Enablement
- 100% provisioned via modular **Terraform** following Azure Landing Zone standards.
- Created reusable CI/CD pipelines in **GitHub Actions** and **Azure DevOps**, enabling frictionless automated testing and blue/green deployments.

## Business Impact & Results
- **Zero Critical Production Incidents** throughout major peak trading windows and Black Friday/Christmas events.
- Reduced pick latency across 600+ stores by over 35%.
- Established the benchmark for cloud-native engineering standards across ASDA's digital engineering teams.
