---
title: "Tier-1 UK Retail Architecture: Infrastructure as Code, CI/CD & Platform Engineering"
category: "Architecture"
company: "Major UK Grocery Retailer (Tier-1 Supermarket Chain) / TCS"
role: "Principal Engineer | Technical Owner & Lead Architect"
startDate: "2023-01"
endDate: "2026-01"
sourceName: "Architecture: Platform Engineering & DevOps"
sourceLink: "#architecture-iac-devops"
technologies:
  - "Terraform (azurerm, azuread)"
  - "Platform Engineering"
  - "Azure DevOps Pipelines"
  - "InnerSource"
  - "Azure Monitor Workbooks as Code"
  - "SonarCloud & GHAS"
  - "W3C Distributed Tracing"
---

# Platform Engineering: Infrastructure as Code & Multi-Stage Deployment Governance

Architected and governed the enterprise **Infrastructure as Code (IaC)** strategy, CI/CD automation pipelines, and Observability-as-Code telemetry for a mission-critical nationwide grocery eCommerce fulfillment platform supporting **700,000+ weekly customer orders across 600+ physical stores**.

## 1. ADR-003: Platform vs Workload Two-Repository Decoupling Model
To establish clear governance boundaries between shared enterprise infrastructure and rapid product iteration, I architected a **two-repository Infrastructure as Code (IaC) model**:

```
┌────────────────────────────────────────────────────────────────────────┐
│ Platform Repository: `ecomm-fulfillment-picking-integration-common`    │
│  • Owns shared infrastructure: Cosmos DB account/DB, shared storage    │
│  • Azure Table reference data, App Service Plan autoscale policies     │
│  • Edge APIM router policies (legacy platform migration routing)       │
└────────────────────────────────────┬───────────────────────────────────┘
                                     │ Terraform `data` source linkage
                                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Workload Repository: `ecomm-fulfillment-picking-integration`           │
│  • Owns compute and integration layer: Azure Functions, APIM APIs      │
│  • In-store edge printing endpoints, dead-letter reprocessors          │
│  • Role assignments: Service Bus Data Receiver, Key Vault Secrets User │
└────────────────────────────────────────────────────────────────────────┘
```

- **Shared State, Separate Lifecycles**: The workload repository references platform-managed resources (e.g. Cosmos DB endpoints, subnets, Key Vaults) via Terraform `data` sources. Workload squads deploy and iterate on business logic without risk to underlying enterprise data foundations.
- **Contract & Spec-Driven API Design**: OpenAPI specifications and JSON Schemas are version-controlled directly within the repository, enabling automated pull request (PR) linting and schema diff validations before gateway deployment.
- **Secure Remote State**: All Terraform state backends use Azure Blob Storage secured exclusively via Azure AD authentication (`use_azuread_auth = true`), eliminating storage access keys from CI/CD pipelines.

## 2. Enterprise CI/CD Progressive Promotion Pipeline Governance
Both repositories utilize **shared enterprise Azure DevOps pipeline templates** (`ado-pipeline-templates`), standardizing compliance and deployment gates across squads:

```
[Developer PR] ──► [Validation Pipeline]
                     • Terraform validate / plan / format check
                     • .NET Build, xUnit Test Suite, SonarCloud & GHAS Scan
                            │
                            ▼ (Merge to main)
                   [Progressive Promotion Pipeline]
                     • DEV: Auto-apply Terraform & deploy Function App
                            │
                            ▼ (Automated integration validation)
                     • TEST / TEST2: Multi-environment verification
                            │
                            ▼ (Manual approval gate)
                     • STAGING: Pre-production verification & load test
                            │
                            ▼ (Senior engineering change sign-off)
                     • PRODUCTION: Zero-downtime apply & post-deploy smoke
```

## 3. Observability as Code: Azure Monitor Workbooks & W3C Distributed Tracing
Rather than configuring operational dashboards manually in the Azure Portal, the platform provisions complete **Azure Monitor Workbooks as Code** (`ecomm-picking-dashboard.workbook`):
- Visualizes real-time API request volumes, error rates, and failure distributions across all 600+ physical stores.
- Tracks store-wise order volumes and Service Bus queue/topic depths.
- Ingests **W3C TraceContext** and `x-correlation-id` headers, allowing on-call engineers to trace a single order from APIM ingress through Service Bus, Function handlers, external SAP calls, and final Cosmos DB persistence.
