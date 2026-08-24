---
title: "Tier-1 UK Retail Architecture: Security Architecture, Private Networking & STRIDE"
category: "Architecture"
company: "Major UK Grocery Retailer (Tier-1 Supermarket Chain) / TCS"
role: "Principal Engineer | Technical Owner & Lead Architect"
startDate: "2023-01"
endDate: "2026-01"
sourceName: "Architecture: Security & Private Networking"
sourceLink: "#architecture-security-network"
technologies:
  - "Azure Entra ID (Azure AD)"
  - "Private Endpoints & Azure Private Link"
  - "VNet Integration (Route-All)"
  - "System-Assigned Managed Identity"
  - "Cosmos DB SQL RBAC"
  - "Azure Key Vault"
  - "STRIDE Threat Modeling"
---

# Zero-Trust Security Architecture & Private Network Isolation

The Enterprise Grocery eCommerce Fulfillment Integration Platform processes sensitive retail customer order data and in-store pick execution state across 600+ physical stores. The security architecture enforces a zero-trust model across public ingress perimeters, private transport networks, compute runtimes, and data-plane storage.

```
[Enterprise OMS / Mobile Picking Client]
       │ TLS 1.2 + Subscription Key + OAuth2 Bearer JWT
       ▼
┌────────────────────────────────────────────────────────────────────────┐
│ APIM Perimeter (Public HTTPS Listener)                                 │
│  • validate-jwt (Entra ID OpenID role claims)                          │
│  • Strips Ocp-Apim-Subscription-Key before downstream dispatch         │
│  • Obtains Managed Identity token for Service Bus (East-West exchange) │
└────────────────────────────────────┬───────────────────────────────────┘
                                     │ AMQP-over-HTTPS (MI Token)
                                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Private Virtual Network (VNet)                                         │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Function App Subnet (VNet Integration, Route-All = true)         │  │
│  │  • System-Assigned Managed Identity                              │  │
│  │  • DefaultAzureCredential for Key Vault Secret References        │  │
│  └──────────────────┬────────────────────────────┬──────────────────┘  │
│                     │ Private Link               │ Private Link        │
│                     ▼                            ▼                     │
│  ┌─────────────────────────────────┐ ┌───────────────────────────────┐ │
│  │ Private Endpoint: Cosmos DB     │ │ Private Endpoint: Storage     │ │
│  │  • Public Network Access: false │ │  • Public Network Access: false│ │
│  │  • Role: Cosmos Data Contributor│ │  • Blob / Table / Queue       │ │
│  └─────────────────────────────────┘ └───────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────┘
```

## 1. Authentication & Token Exchange Patterns
- **North-South Edge Security**: Inbound client requests to APIM require both an API subscription key and an OAuth2 Bearer JWT issued by Entra ID. APIM validates the `aud` (audience) and granular `roles` claims (e.g. `Ecommerce.Order.Create`, `Ecommerce.PickingEvent.Update`).
- **East-West Token Exchange**: APIM explicitly removes the client's subscription key from headers before forwarding. It acquires its own **System-Assigned Managed Identity token** to write to Azure Service Bus. This prevents third-party credentials from leaking into internal message queues.
- **Least-Privilege Data Plane RBAC**: Function Apps authenticate to Cosmos DB via Azure AD Managed Identity assigned the native `Cosmos DB Built-in Data Contributor` role definition. No master database keys or shared access keys are used in application code.

## 2. Private Networking & Infrastructure Hardening
- **Private Endpoints & Azure Private Link**: Public network access is disabled (`public_network_access_enabled = false`) on Azure Cosmos DB, Azure Storage accounts, and internal Function App listeners.
- **VNet Integration with Route-All**: The Azure Function App is deployed inside a dedicated subnet with `vnet_route_all_enabled = true`, forcing all outbound egress traffic (including external adapter calls to SAP and in-store picking systems) through corporate virtual networks and inspection firewalls.
- **Secret Management**: All third-party system secrets, API keys, and certificates are stored in Azure Key Vault and referenced at runtime using `@Microsoft.KeyVault(SecretUri=...)` app settings.

## 3. STRIDE Threat Model & Mitigations

| Threat | Risk Vector | Mitigation Implemented |
| :--- | :--- | :--- |
| **Spoofing** | Forged client calling OMS / Store Picking APIs | APIM `validate-jwt` against Entra ID OpenID metadata + role claim verification |
| **Tampering** | Payload interception/modification in transit | Enforced TLS 1.2, JSON schema validation at gateway, and private VNet links |
| **Repudiation** | Disputed order state or pick events | End-to-end W3C correlation ID tracking + immutable Cosmos DB audit records |
| **Information Disclosure** | Data plane database exposure | Public access disabled; Azure Private Endpoints; Key Vault secret references |
| **Denial of Service** | Flash traffic surging on single store partition | Sharded Session Pattern (`Random.Next(1,5)`) + Cosmos DB autoscale (48k RU/s) |
| **Elevation of Privilege** | Compromised Function compute instance | Least-privilege data plane SQL RBAC roles; Key Vault `Get`-only permissions |
