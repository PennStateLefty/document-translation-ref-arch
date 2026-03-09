<!--
  Sync Impact Report
  ==================
  Version change: N/A → 1.0.0 (initial adoption)
  Modified principles: N/A (initial adoption)
  Added sections:
    - Core Principles (6 principles)
    - Technology Stack
    - Development Workflow
    - Governance
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ compatible (Constitution
      Check section is a generic gate; no updates needed)
    - .specify/templates/spec-template.md ✅ compatible (spec structure
      aligns with principle-driven requirements)
    - .specify/templates/tasks-template.md ✅ compatible (phase-based
      task structure supports IaC-first and CI/CD gates)
    - .specify/templates/checklist-template.md ✅ compatible (generic
      checklist format; no principle-specific conflicts)
    - .specify/templates/agent-file-template.md ✅ compatible (no
      outdated agent-specific references)
  Follow-up TODOs: None
-->

# Document Translation Reference Architecture Constitution

## Core Principles

### I. Infrastructure-as-Code First

All Azure resources MUST be defined in Bicep templates. No Azure
Portal click-ops or manual provisioning is permitted.

- Every resource (compute, storage, networking, identity) MUST have
  a corresponding Bicep definition under the `infra/` directory.
- Environment-specific configuration MUST be expressed as Bicep
  parameters, never as portal-level overrides.
- Changes to infrastructure MUST follow the same PR review process
  as application code.
- **Rationale**: Reproducibility and auditability are non-negotiable
  for a reference architecture. If it cannot be reproduced from
  code, it is not a valid reference.

### II. Reference Architecture Clarity

Architectural guidance documentation and reference implementation
code MUST be clearly separated and independently consumable.

- Documentation MUST explain *why* an architectural decision was
  made, not just *what* was implemented.
- The reference implementation MUST be a concrete, runnable example
  of the documented architecture—not a standalone product.
- Readers MUST be able to understand the architecture from the docs
  alone, and run the implementation without reading the docs.
- **Rationale**: The primary audience is audit professionals and
  solution architects evaluating patterns. Mixing guidance with
  implementation creates confusion about what is prescriptive
  versus illustrative.

### III. Testability & CI/CD

The project MUST maintain adequate testing and automated pipelines
to enable confident deployment of every change.

- Pull requests MUST pass CI validation gates before merge.
- Dependency security scanning (e.g., Dependabot) MUST be enabled
  and alerts MUST be triaged.
- Tests MUST cover critical orchestration paths (Durable Functions
  fan-out/fan-in, batch splitting logic).
- Test quality targets non-production adequacy: demonstrate correct
  patterns rather than exhaustive production-grade coverage.
- **Rationale**: A reference architecture that cannot be validated
  automatically teaches bad habits. CI/CD is part of the pattern
  being demonstrated.

### IV. Simplicity & Pattern Focus

Implementations MUST remain simple and non-production-grade,
focused on demonstrating scalable architectural patterns.

- Favor clarity over cleverness; every code path MUST demonstrate
  a specific pattern (Durable Functions fan-out/fan-in, batch
  orchestration, Storage event handling).
- Abstractions MUST be justified: if a wrapper does not clarify
  a pattern, remove it.
- Production concerns (advanced auth, multi-tenancy, custom
  domains) MUST NOT be added unless they directly illustrate
  a documented architectural pattern.
- **Rationale**: Complexity obscures the patterns this reference
  architecture exists to teach. YAGNI applies aggressively.

### V. Azure Developer CLI Native

The complete environment lifecycle MUST be manageable via Azure
Developer CLI (`azd`) commands.

- `azd up` MUST provision all infrastructure and deploy all
  application components from a clean state.
- `azd down` MUST tear down all provisioned resources cleanly.
- The `azure.yaml` manifest MUST be the single source of truth
  for service-to-infrastructure mappings.
- Manual setup steps outside of `azd` are prohibited; any
  prerequisite MUST be automated or clearly documented as a
  one-time `azd` environment configuration.
- **Rationale**: `azd` native support makes the reference
  architecture accessible to evaluators who need to stand up
  and tear down environments quickly without deep Azure CLI
  knowledge.

### VI. Scalability by Design

The architecture MUST demonstrate patterns for handling
large-scale document translation workloads.

- Durable Functions MUST be used for long-running orchestration
  with proper checkpoint and retry semantics.
- Batch processing MUST handle Azure Document Translation service
  limits (1000 files per batch, 250MB per batch) by automatically
  splitting oversized jobs.
- The architecture MUST support configuring multiple Document
  Translation endpoints for throughput scaling.
- Parallel processing via fan-out/fan-in MUST be the default
  orchestration strategy for batch translation jobs.
- **Rationale**: The target use case—audit professionals receiving
  large tranches of documents—demands patterns that scale beyond
  single-request limits. Demonstrating these patterns is the
  project's primary value.

## Technology Stack

The following technologies are mandated for this project. Deviations
require a constitution amendment.

| Layer | Technology | Constraints |
|-------|-----------|-------------|
| Frontend | React | Drag-and-drop multi-file upload UX |
| Backend | C# / .NET (Azure Functions, Isolated worker model) | Durable Functions framework for orchestration |
| Translation | Azure Document Translation (via Microsoft Foundry) | Batch API; respect 1000-file / 250MB limits |
| Storage | Azure Blob Storage | Source and target containers for translation I/O |
| IaC | Bicep | All resources under `infra/`; no ARM JSON |
| Tooling | Azure Developer CLI (`azd`) | `azure.yaml` at repo root |
| Repo | Monorepo | Frontend + Backend + IaC in a single repository |

## Development Workflow

### Commit Conventions

All commits MUST follow the [Conventional Commits](https://www.conventionalcommits.org/)
specification.

- Format: `<type>(<scope>): <description>`
- Permitted types: `feat`, `fix`, `docs`, `style`, `refactor`,
  `test`, `chore`, `ci`, `build`
- Scopes SHOULD align with project areas: `frontend`, `backend`,
  `infra`, `azd`, `docs`, `ci`

### Pull Request Process

- Every change MUST be submitted via pull request.
- PRs MUST pass all CI validation gates (build, test, lint) before
  merge is permitted.
- PRs MUST receive at least one approving review.
- PR titles MUST follow Conventional Commits format.

### CI Gates

- Build validation for both frontend and backend on every PR.
- Automated test execution for all test suites on every PR.
- Dependency security scanning (Dependabot or equivalent) MUST be
  active with alerts triaged within one business week.
- Bicep linting and validation MUST run on PRs that modify `infra/`.

## Governance

This constitution is the supreme governing document for the Document
Translation Reference Architecture project. All development
decisions, code reviews, and architectural changes MUST comply with
the principles defined herein.

### Amendment Procedure

1. Propose an amendment via a pull request modifying this file.
2. The PR description MUST include: the principle(s) affected,
   rationale for the change, and impact assessment on existing
   implementation.
3. Amendments require at least one approving review from a project
   maintainer.
4. Upon merge, the `LAST_AMENDED_DATE` and `CONSTITUTION_VERSION`
   MUST be updated per the versioning policy below.

### Versioning Policy

This constitution follows semantic versioning (MAJOR.MINOR.PATCH):

- **MAJOR**: Backward-incompatible governance changes—principle
  removals, redefinitions that invalidate existing implementations.
- **MINOR**: New principles or sections added, material expansions
  of existing guidance.
- **PATCH**: Clarifications, wording improvements, typo fixes,
  non-semantic refinements.

### Compliance Review

- All PR reviews MUST verify compliance with this constitution.
- Complexity additions MUST be justified against Principle IV
  (Simplicity & Pattern Focus) in the PR description.
- Infrastructure changes MUST be validated against Principle I
  (Infrastructure-as-Code First) and Principle V (Azure Developer
  CLI Native).

**Version**: 1.0.0 | **Ratified**: 2026-03-04 | **Last Amended**: 2026-03-04
