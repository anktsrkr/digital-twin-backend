---
title: "Project Showcase: Enterprise Agentic Developer Tooling & Custom Copilot Agents"
category: "Projects"
company: "Enterprise AI Enablement & Developer Platforms"
role: "Principal Engineer | Lead AI Architect"
startDate: "2023-01"
endDate: "Present"
sourceName: "Project: Agentic Developer Tooling"
sourceLink: "#project-copilot-agents"
technologies:
  - "GitHub Copilot Agents"
  - "Custom Agent Skills"
  - "VS Code Customization Primitives"
  - "Microsoft Agent Framework"
  - ".NET 6 to .NET 10 Migration"
  - "Azure Functions Isolated Worker"
  - "Developer Experience (DevEx)"
  - "Terraform Automation"
  - "Token Efficiency Optimization"
  - "Deterministic Agent Orchestration"
---

# Project Showcase: Enterprise Agentic Developer Tooling & Copilot Skills

Designed, engineered, and scaled custom **GitHub Copilot Agents**, reusable **Agent Skills**, and deterministic one-click prompt workflows across **100+ enterprise microservice repositories**, slashing manual cloud migration effort from days to minutes while guaranteeing zero build failures.

## 1. Challenge: Multi-Repo Modernization at Enterprise Scale
Migrating an estate of 100+ Azure Functions repositories from **.NET 6 in-process to .NET 10 isolated-worker** required mechanical code rewrites, NuGet package resolution, Terraform provider updates, Azure DevOps CI/CD modifications, and structured logging swaps.

Naive monolithic prompts failed with hallucinations and cross-phase reasoning interference (e.g. applying Terraform logic while editing C# files).

## 2. Technical Solution: Deterministic Multi-Agent & Skill System
Engineered a modular architecture using VS Code Copilot primitives (`agent`, `prompt`, `instructions`, and `skill`):

- **`/dotnet10-modernize` One-Click Prompt**: Single entry point that pins the orchestrator agent and defines strict Definition of Done criteria.
- **Thin Orchestrator Agent (`dotnet10-modernize.agent.md`)**: Enforces phase sequence, gating preconditions, and git commit hygiene with zero hardcoded migration domain knowledge.
- **6 Phase-Scoped Agent Skills**:
  1. `skill: feed-availability` — Blocking Phase 0 gate verifying NuGet feeds before any work begins.
  2. `skill: dependency-preflight` — Resolves package dependencies upfront with a single consolidated `.csproj` edit.
  3. `skill: isolated-worker-conversion` — Mechanical C# conversion (`Startup.cs` $\rightarrow$ `Program.cs`, trigger bindings).
  4. `skill: verification-gate` — Re-runnable, idempotent validation checking 6 discrete build/test/lint invariants.
  5. `skill: terraform-upgrade` + `skill: platform-logger-migration` — Upgrades Terraform compute resources and modernizes logging facades.
  6. `skill: pipeline-migration` — Updates Azure DevOps YAML pipelines to target .NET 10 SDKs.
- **Living Architecture Generator (`repo-flow-knowledge-reference`)**: Standalone companion skill that treats Terraform as the authoritative topology source to generate living `function-flow-reference.md` documentation.

## 3. Engineering Rigor & Token Optimization
- **Grep-First Live Discovery**: Zero remembered assumptions; verifies trigger types and packages fresh via `git grep -n`, eliminating speculation.
- **Direct CLI Execution**: Calls native CLIs directly (`dotnet build`, `git grep`, `terraform`) with no wrapper PowerShell scripts, ensuring full auditability and least privilege.
- **External Memory (`progress.md`)**: Persists decision tables and gate results to a tracking file, reducing token consumption by **>75%**.

## 4. Measurable Business Impact
- **Automated 100+ Microservice Migrations**: Cut migration effort from days per repository to under **15 minutes** of automated agent execution.
- **100% Build & Test Pass Rate**: Phased context isolation and live verification gates ensured zero broken PRs across squads.
