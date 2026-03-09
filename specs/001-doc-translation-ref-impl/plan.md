# Implementation Plan: Document Translation Reference Implementation

**Branch**: `001-doc-translation-ref-impl` | **Date**: 2025-07-17 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-doc-translation-ref-impl/spec.md`

## Summary

Build a reference architecture demonstrating scalable document translation
on Azure. A React frontend provides drag-and-drop multi-file upload with
target language selection; a C# .NET 8 Azure Functions backend uses Durable
Functions fan-out/fan-in orchestration to split large uploads into batches
(respecting 1,000 file / 250 MB limits) and invoke Azure Document
Translation's batch API in parallel. The frontend polls a status endpoint
at 5-second intervals and presents a download button on completion. All
infrastructure is defined in Bicep, deployable via `azd up`, with CI/CD
via GitHub Actions.

## Technical Context

**Language/Version**: C# / .NET 8 (backend), TypeScript / React 18 (frontend), Bicep (IaC)
**Primary Dependencies**:
- Backend: `Microsoft.Azure.Functions.Worker`, `Microsoft.Azure.Functions.Worker.Extensions.DurableTask`, `Azure.AI.Translation.Document`, `Azure.Storage.Blobs`, `Azure.Identity`
- Frontend: `react`, `react-dom`, `react-dropzone`, `vite`, `typescript`
**Storage**: Azure Blob Storage (source + translated document containers; Durable Functions internal storage)
**Testing**: `dotnet test` (xUnit) for backend, `vitest` for frontend
**Target Platform**: Azure (Function App + Static Web App + Storage + AI Translator)
**Project Type**: Web application (monorepo: React SPA frontend + Azure Functions API backend + Bicep IaC)
**Performance Goals**: Non-production reference implementation; no specific throughput targets. SC-001 targets session initiation under 2 minutes.
**Constraints**: Azure Document Translation limits: 1,000 files/batch, 250 MB/batch, 100 MB/file. Non-production grade (no auth, no multi-tenancy).
**Scale/Scope**: Single-user reference implementation demonstrating patterns for large document sets (2,500+ files). 1 frontend page, ~4 API endpoints, ~6 Azure Functions, ~7 Bicep modules.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research Gate (Phase 0 Entry)

| # | Principle | Requirement | Status |
|---|-----------|-------------|--------|
| I | Infrastructure-as-Code First | All resources in Bicep under `infra/` | ✅ PASS — Plan defines `infra/` with Bicep modules for all resources |
| II | Reference Architecture Clarity | Docs explain *why*, impl is runnable independently | ✅ PASS — Spec separates docs from impl; quickstart enables standalone use |
| III | Testability & CI/CD | CI gates on PRs, tests for critical paths | ✅ PASS — Plan includes PR validation workflow + xUnit/vitest test suites |
| IV | Simplicity & Pattern Focus | No production concerns; every path demonstrates a pattern | ✅ PASS — No auth, no multi-tenancy; each component teaches fan-out/fan-in, batch splitting, or IaC patterns |
| V | Azure Developer CLI Native | `azd up` / `azd down` lifecycle | ✅ PASS — `azure.yaml` at root with two services; Bicep outputs feed environment |
| VI | Scalability by Design | Durable Functions, batch splitting, fan-out/fan-in | ✅ PASS — Orchestrator fans out to per-batch activities; automatic splitting at 1,000/250MB |

**Gate Result**: ✅ ALL PASS — proceed to Phase 0 research.

### Post-Design Re-check (Phase 1 Complete)

| # | Principle | Design Artifact | Status |
|---|-----------|-----------------|--------|
| I | IaC First | `infra/` layout in Project Structure below; 7 Bicep modules | ✅ PASS |
| II | Ref Arch Clarity | `quickstart.md` separates "how to run" from "why it's built this way" | ✅ PASS |
| III | Testability | Tests cover batch splitting + fan-out/fan-in (data-model.md defines testable split rules) | ✅ PASS |
| IV | Simplicity | No database (orchestration state only); no auth; minimal frontend | ✅ PASS |
| V | azd Native | `azure.yaml` maps `web` → Static Web App, `api` → Function App | ✅ PASS |
| VI | Scalability | Data model defines batch splitting rules; contracts define multi-batch status reporting | ✅ PASS |

**Gate Result**: ✅ ALL PASS — design is constitution-compliant.

## Project Structure

### Documentation (this feature)

```text
specs/001-doc-translation-ref-impl/
├── plan.md              # This file
├── research.md          # Phase 0 output — technology decisions and rationale
├── data-model.md        # Phase 1 output — entities, state machines, blob layout
├── quickstart.md        # Phase 1 output — deployment and usage guide
├── contracts/
│   └── api.md           # Phase 1 output — REST API contract
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
azure.yaml                          # azd manifest — service-to-infra mappings

infra/                              # Bicep IaC (Constitution Principle I)
├── main.bicep                      # Orchestrator — imports all modules
├── main.parameters.json            # Default parameters (env name, location)
└── modules/
    ├── function-app.bicep           # Azure Functions (Consumption/Flex plan)
    ├── static-web-app.bicep         # Azure Static Web Apps (React hosting)
    ├── storage.bicep                # Storage Account (docs + Functions runtime)
    ├── translator.bicep             # Azure AI Translator (Cognitive Services)
    ├── monitoring.bicep             # Application Insights + Log Analytics
    └── role-assignments.bicep       # Managed identity RBAC bindings

src/
├── api/                            # C# Azure Functions backend
│   ├── DocumentTranslation.Api.csproj
│   ├── Program.cs                  # Host builder + DI configuration
│   ├── host.json                   # Durable Functions + logging config
│   ├── Models/
│   │   ├── TranslationSession.cs
│   │   ├── TranslationBatch.cs
│   │   ├── SourceDocument.cs
│   │   ├── TranslationResult.cs
│   │   └── Enums.cs                # TranslationStatus, BatchStatus
│   ├── Functions/
│   │   ├── TranslateHttpTrigger.cs         # POST /api/translate
│   │   ├── StatusHttpTrigger.cs            # GET /api/translate/{sessionId}
│   │   ├── DownloadHttpTrigger.cs          # GET /api/translate/{sessionId}/download
│   │   ├── LanguagesHttpTrigger.cs         # GET /api/languages
│   │   └── TranslationOrchestrator.cs      # Durable orchestrator + activities
│   ├── Services/
│   │   ├── IBlobStorageService.cs
│   │   ├── BlobStorageService.cs
│   │   ├── ITranslationService.cs
│   │   └── TranslationService.cs
│   └── DocumentTranslation.Api.Tests/
│       ├── DocumentTranslation.Api.Tests.csproj
│       ├── BatchSplitterTests.cs           # Critical: batch splitting logic
│       ├── OrchestratorTests.cs            # Critical: fan-out/fan-in flow
│       └── ValidationTests.cs             # Upload validation rules
│
└── web/                            # React frontend
    ├── package.json
    ├── vite.config.ts
    ├── tsconfig.json
    ├── staticwebapp.config.json     # SWA routing + API proxy
    ├── index.html
    ├── src/
    │   ├── App.tsx
    │   ├── main.tsx
    │   ├── types/
    │   │   └── translation.ts       # TypeScript interfaces matching API contract
    │   ├── components/
    │   │   ├── FileUpload.tsx        # Drag-and-drop upload area
    │   │   ├── LanguageSelector.tsx  # Target language dropdown
    │   │   ├── TranslationStatus.tsx # Status display with auto-polling
    │   │   ├── DownloadButton.tsx    # Download translated files
    │   │   └── ErrorMessage.tsx      # Error display component
    │   ├── hooks/
    │   │   ├── useTranslation.ts     # Upload + start translation
    │   │   └── usePolling.ts         # 5-second status polling
    │   └── services/
    │       └── apiClient.ts          # HTTP client for backend API
    └── tests/
        ├── FileUpload.test.tsx
        └── usePolling.test.ts

.github/
└── workflows/
    ├── ci.yml                       # PR validation: build + test + lint
    └── bicep-validate.yml           # Bicep lint (on infra/** changes)

.github/dependabot.yml               # Dependency security scanning
```

**Structure Decision**: Web application monorepo layout with `src/api/` (C#
backend) and `src/web/` (React frontend) at the same level under `src/`.
Infrastructure lives in `infra/` at the repo root. This mirrors the canonical
`azd` template structure and aligns with the constitution's monorepo mandate.
The `api` and `web` naming convention matches the `azure.yaml` service names
for clarity.

## Complexity Tracking

> No constitution violations detected. All design decisions align with the six
> core principles. No complexity justifications required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *(none)* | — | — |
