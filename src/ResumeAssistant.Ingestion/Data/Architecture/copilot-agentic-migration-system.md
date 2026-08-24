---
title: "Agentic AI Architecture: Deterministic Multi-Repo .NET 10 Migration Agent for GitHub Copilot"
category: "Architecture"
company: "Enterprise AI Engineering & Developer Platforms"
role: "Principal Engineer | AI Solutions Architect"
startDate: "2024-01"
endDate: "Present"
sourceName: "Architecture: Multi-Repo Copilot Migration Agent"
sourceLink: "#architecture-copilot-agent"
technologies:
  - "GitHub Copilot Agents"
  - "Agentic AI"
  - "VS Code Customization Primitives"
  - "Agent Skills"
  - ".NET 6 to .NET 10 Migration"
  - "Azure Functions Isolated Worker"
  - "Prompt Engineering & Guardrails"
  - "Token Efficiency Optimization"
  - "Deterministic Agent Orchestration"
---

# Deterministic Multi-Repo Modernization Agent for GitHub Copilot

Enterprise Azure Functions estates spanning **100+ microservice repositories** required a repeatable, automated migration from **.NET 6 in-process to .NET 10 isolated-worker model**. 

Migrating a single Function App touches five distinct concerns: NuGet package resolution, mechanical C# code rewrites (`Startup.cs` $\rightarrow$ `Program.cs`, trigger bindings), Terraform provider upgrades, Azure DevOps CI/CD pipeline changes, and structured logging library swaps. Monolithic prompts that loaded all rules simultaneously produced severe hallucinations and cross-phase reasoning errors. To solve this, I designed a **deterministic, phase-gated, skill-based multi-agent system** leveraging VS Code GitHub Copilot customization primitives (`agent`, `prompt`, `instructions`, and `skill`).

```
┌────────────────────────────────────────────────────────────────────────┐
│             DETERMINISTIC COPILOT MIGRATION ARCHITECTURE               │
├────────────────────────────────────────────────────────────────────────┤
│ 1. Entry Point: `/dotnet10-modernize` Prompt                           │
│    • Restates Definition of Done & invokes thin Orchestrator Agent     │
├────────────────────────────────────────────────────────────────────────┤
│ 2. Orchestrator Agent (`dotnet10-modernize.agent.md`)                  │
│    • Owns phase order, gating preconditions, and git commit hygiene    │
│    • Contains zero migration domain knowledge (pure coordinator)       │
├────────────────────────────────────────────────────────────────────────┤
│ 3. Phase-Gated Skills (Phase-Scoped Domain Knowledge)                  │
│    ├── Phase 0: `skill: feed-availability` (Blocking Precondition)     │
│    ├── Phase 1: Git Status & Branch Hygiene Check                      │
│    ├── Phase 2: `skill: dependency-preflight` (NuGet Resolution)       │
│    ├── Phase 3: `skill: isolated-worker-conversion` (C# Code Rewrite)  │
│    ├── Phase 4: `skill: verification-gate` (Build & Test Check 1)      │
│    ├── Phase 5: `skill: terraform-upgrade` + `skill: platform-logger`  │
│    ├── Phase 6: `skill: pipeline-migration` (Azure DevOps YML)        │
│    ├── Phase 7: `skill: verification-gate` (Final Verification Gate)   │
│    └── Phase 8: Consolidated `progress.md` + Scoped Git Commit         │
├────────────────────────────────────────────────────────────────────────┤
│ 4. Always-On Guardrails (`dotnet10-modernize.instructions.md`)         │
│    • Auto-attached via `applyTo: ["*.cs", "*.csproj", "*.tf", "*.yml"]`│
│    • Global invariants: net10.0 only, no PowerShell, live discovery   │
└────────────────────────────────────────────────────────────────────────┘
```

## 1. Core Architectural Primitives & Component Map

| Primitive | File Pattern | Loading Trigger | Architectural Responsibility |
| :--- | :--- | :--- | :--- |
| **Agent** | `dotnet10-modernize.agent.md` | Invoked directly or via `/dotnet10-modernize` | **Thin Orchestrator**: Enforces phase sequence, gate checks, and git commit scoping. Contains zero domain rules. |
| **Prompt** | `dotnet10-modernize.prompt.md` | User runs `/dotnet10-modernize` | **One-Click Entry Point**: Pins the orchestrator and states the Definition of Done. |
| **Instructions** | `dotnet10-modernize.instructions.md` | Auto-loaded when matching files open (`applyTo`) | **Global Guardrails**: Target .NET 10 only, no `.ps1` scripts, never resolve package versions from memory. |
| **Skills** | `.github/skills/dotnet10-modernize-*/SKILL.md` (6 skills) | Explicitly invoked by name by orchestrator | **Phase-Scoped Domain Knowledge**: Mapping tables, grep patterns, and fix recipes loaded strictly on-demand. |
| **Living Knowledge** | `repo-flow-knowledge-reference` (`function-flow-reference.md`) | Standalone invoked skill | **Living Architecture Doc**: Generates living topology maps by treating Terraform as source of truth. |

## 2. Direct CLI Calls vs Bundled PowerShell Scripts (Auditability & Least Privilege)
- **Decision**: Strict prohibition of `.ps1` wrapper scripts; the agent must invoke installed CLIs directly (`dotnet build`, `git grep -n`, `terraform validate`).
- **Rationale**:
  - **Auditability**: Direct CLI calls output transparent, self-describing logs in the transcript. Wrapper scripts hide failures behind opaque internal state.
  - **Zero Maintenance Overhead**: Eliminates the need to maintain, test, and sign helper scripts across 100+ repositories.
  - **Least Privilege & Inspectability**: Disposable one-shot CLI commands have zero persistent footprint and minimal blast radius.

## 3. Live Grep/Glob Discovery vs Remembered State (Eliminating Speculative Hallucination)
- **Decision**: The agent must re-discover facts fresh via targeted `git grep -n` on every pass, never trusting previous in-memory assumptions.
- **Rationale**:
  - **Handles Multi-Repo Drift**: Across 100+ repositories, trigger types and package versions vary widely.
  - **Turns "Trust Me" into "Verify This"**: Gating package additions behind `git grep` hits prevents speculative hallucination (e.g. adding Blob storage packages to an app that only uses Service Bus).
  - **Reuses Discovery Patterns as Verification Criteria**: The exact grep patterns used to find legacy WebJobs references (`Microsoft.Azure.WebJobs`, `FunctionsStartup`) serve as the "must return 0 hits" exit criteria in the verification gate.

## 4. Token Efficiency & Context Window Budgeting (>75% Token Reduction)
- **Narrow On-Demand Skill Loading**: Loading only 1 skill at a time (~500 tokens) rather than a giant 8,000-token prompt reduces LLM inference cost by **>75%**.
- **External Memory Tracking (`progress.md`)**: Package versions, feed availability, and gate checklists are persisted to `progress.md`. Subsequent phases read the file instead of burning tokens re-justifying past decisions.
- **Bounded Retries**: Hard caps on retries (max 2 for NuGet conflicts, max 3 for Terraform `init` transient network blips) prevent runaway token consumption.
- **One Consolidated Edit Pass**: Resolves all package dependencies upfront and applies a single consolidated `.csproj` edit rather than an expensive edit-check-edit cycle.

## 5. Quantified Business Outcomes & Enterprise Impact
- **100+ Enterprise Repositories Modernized**: Enabled consistent, automated modernization from .NET 6 to .NET 10 across distributed teams.
- **Zero Hallucination Rate**: Phase-scoped context isolation and live verification gates achieved 100% build-and-test pass rates on generated PRs.
- **Living Architecture Generation**: The companion `repo-flow-knowledge-reference` skill automatically generates accurate, Terraform-verified architecture documentation (`function-flow-reference.md`) for every repository.
