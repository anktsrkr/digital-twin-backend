---
title: "Major UK Grocery Retailer (Tier-1 Supermarket): Azure eCommerce Picking Platform"
category: "Experience"
company: "Tata Consultancy Services (Major UK Grocery Retailer / Tier-1 Supermarket Chain)"
role: "Principal Engineer | Technical Owner & SME (eCommerce Fulfillment)"
startDate: "2023-01"
endDate: "2026-01"
sourceName: "Work Experience: Tier-1 UK Grocery & Retail eCommerce Platform"
sourceLink: "#experience-grocery"
technologies:
  - "Retail eCommerce"
  - "Grocery Fulfilment"
  - "Tier-1 Supermarket"
  - "Microsoft Azure"
  - "C#"
  - ".NET"
  - "Event-Driven Architecture"
  - "Azure Well-Architected Framework"
  - "Azure Integration Services"
  - "Service Bus"
  - "Event Grid"
  - "Event Hubs"
  - "Azure Functions"
  - "Durable Task"
  - "Cosmos DB"
  - "Redis Cache"
  - "Terraform"
  - "DevSecOps"
  - "InnerSource"
---

# Technical Ownership & Platform Engineering for UK Grocery Fulfilment

Served as the Technical Owner and Subject Matter Expert (SME) for a Major UK Grocery Retailer's (Tier-1 Supermarket Chain) Azure-based nationwide eCommerce grocery picking platform, accountable for platform strategy, architectural evolution, DevSecOps pipelines, and production operations supporting **700,000+ weekly customer orders across 600+ physical stores**.

## 1. High-Scale Platform Architecture & Resilience
- Designed and delivered a highly available, event-driven Azure architecture adhering to the **Azure Well-Architected Framework**.
- Successfully sustained extreme peak flash promotional surges including **90,000+ orders within 30 minutes** and **150,000+ Christmas peak orders** with **zero critical production incidents (P1/P2)**.
- Architected multi-temperature picking wave splitting (Ambient, Chilled, Frozen) using Azure Functions and the Durable Task Framework, synchronizing thousands of simultaneous store pickers.

## 2. Distributed Messaging & Tiered Data Strategy
- Decoupled high-volume digital storefront orders from physical store execution using **Azure Service Bus Premium** session-based queues and **Azure Event Grid**.
- Optimized in-store handheld scanner response times using **Azure Cache for Redis** for sub-millisecond pick state retrieval, and **Azure Cosmos DB** (partitioned by `/StoreId`) for immutable order auditing and item substitution trails.
- Implemented automated Dead Letter Queue (DLQ) processing and idempotent consumers, preventing duplicate pick operations and eliminating order drop risks.

## 3. Engineering Standards, InnerSource & DevEx
- Authored shared **Terraform modules** and **Azure DevOps / GitHub Actions** CI/CD templates, cutting service deployment times from days to under 30 minutes.
- Promoted **InnerSource** development practices across distributed squads, enabling multi-team collaboration and automated security compliance scanning.
- Established end-to-end distributed observability using **W3C TraceContext**, Azure Application Insights, and Log Analytics, cutting Mean Time to Resolution (MTTR) by 50%.
