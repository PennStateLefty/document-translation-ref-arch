# Research: Document Translation Reference Implementation

**Date**: 2025-07-17
**Feature Branch**: `001-doc-translation-ref-impl`

## Research Summary

All NEEDS CLARIFICATION items from Technical Context resolved below.

---

## R-001: Azure Document Translation Batch API

**Decision**: Use the Azure AI Translator Document Translation batch API
(`/translator/document/batches`) with managed identity authentication.

**Rationale**: The batch API is the only Azure translation offering that
supports translating entire documents at scale while preserving formatting.
Managed identity eliminates the need to manage API keys or SAS tokens for
service-to-service communication, aligning with Constitution Principle IV
(Simplicity & Pattern Focus).

**Alternatives Considered**:
- Text Translation API (single-string): Rejected—requires extracting text
  from documents, loses formatting, adds complexity.
- Third-party translation services: Rejected—out of scope for an Azure
  reference architecture.
- Direct REST calls vs. Azure SDK: Decision to use the Azure SDK
  (`Azure.AI.Translation.Document`) for type safety and built-in retry
  support; raw REST is a valid alternative but adds boilerplate.

**Key Technical Details**:
- Endpoint: `POST https://{endpoint}/translator/document/batches?api-version=2024-05-01`
- Source/target containers referenced by SAS URL in the request body
- Service limits: 1,000 files per batch, 250 MB per batch, 100 MB per file
- Status values: `NotStarted` → `Running` → `Succeeded` / `Failed` / `Cancelled`
- Polling endpoint: `GET /translator/document/operations/{operationId}`
- Supported formats: PDF, DOCX, XLSX, PPTX, HTML, TXT, XLF/XLIFF, TSV, and others

**Note on "Microsoft Foundry"**: The spec references "Azure Document
Translation via Microsoft Foundry." Azure AI Foundry is Microsoft's unified
platform for AI services including Azure AI Translator. The underlying API
is the Azure AI Translator Document Translation batch API. The Bicep
resource type is `Microsoft.CognitiveServices/accounts` with kind
`TextTranslation`.

---

## R-002: Durable Functions Orchestration Pattern

**Decision**: Use .NET 8 Isolated Worker Durable Functions with fan-out/fan-in
pattern. Single orchestrator fans out to per-batch activity functions that each
invoke the Document Translation batch API.

**Rationale**: The .NET Isolated worker model is Microsoft's recommended
approach for new Azure Functions projects (in-process model is on a
deprecation path). Durable Functions provides built-in checkpoint/replay,
retry policies, and HTTP status query APIs—directly satisfying FR-008,
FR-011, and FR-016.

**Alternatives Considered**:
- In-process Azure Functions: Rejected—deprecated path; isolated model
  is future-proof.
- Azure Logic Apps: Rejected—less control over orchestration, harder to
  test locally, adds a service dependency.
- Custom queue-based orchestration: Rejected—reinvents what Durable
  Functions provides natively.
- Sub-orchestrations for batch processing: Considered but deferred—for the
  expected scale of this reference implementation (tens of batches, not
  thousands), a flat fan-out is simpler and sufficient per Principle IV.

**Key Technical Details**:
- NuGet packages:
  - `Microsoft.Azure.Functions.Worker` (~1.22)
  - `Microsoft.Azure.Functions.Worker.Sdk` (~1.17)
  - `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` (~1.1)
  - `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` (~1.3)
  - `Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs` (~6.4)
  - `Azure.AI.Translation.Document` (~2.0)
  - `Azure.Storage.Blobs` (~12.21)
  - `Azure.Identity` (~1.12)
- Orchestrator returns `DurableTaskClient.CreateCheckStatusResponse()` for
  built-in status polling via HTTP management APIs
- Retry policy: exponential backoff, 3 attempts, 5 s first interval,
  2.0 backoff coefficient, 5 min max interval

**Orchestration Flow**:
```
HTTP Trigger (StartTranslation)
  → Orchestrator (TranslationSessionOrchestrator)
    → Activity: UploadFilesToBlob
    → Activity: AssessAndSplitBatches
    → Fan-out: [Activity: TranslateBatch] × N
    → Fan-in: Aggregate results
    → Activity: FinalizeSession
```

---

## R-003: Azure Developer CLI (azd) Project Structure

**Decision**: Use `azure.yaml` at repo root with two services (`web` and
`api`). Infrastructure defined in `infra/` using Bicep modules. React
frontend deployed to Azure Static Web Apps; C# backend deployed to Azure
Functions.

**Rationale**: This is the canonical azd monorepo pattern. Static Web Apps
provides free hosting with built-in CI/CD for React, and its linked backend
feature proxies API calls to the Function App, avoiding CORS configuration
complexity.

**Alternatives Considered**:
- Azure App Service for frontend: Rejected—overprovisioned for a static
  React app; Static Web Apps is simpler and free-tier eligible.
- Azure Container Apps: Rejected—adds container orchestration complexity
  without benefit for this use case (Principle IV).
- Separate repos for frontend and backend: Rejected—constitution mandates
  monorepo.

**Key Technical Details**:
- `azure.yaml` services: `web` (staticwebapp) + `api` (function)
- `infra/` layout: `main.bicep` orchestrator, `main.parameters.json`,
  `modules/` for each resource type
- Bicep modules: function-app, static-web-app, storage, translator,
  monitoring, managed-identity-roles
- azd lifecycle: `azd up` = provision + deploy; `azd down` = teardown
- Bicep outputs become azd environment variables for cross-service config

---

## R-004: React Frontend Architecture

**Decision**: Vite + React 18 + TypeScript. Use `react-dropzone` for
drag-and-drop file upload. Files uploaded through backend API (not direct
to Blob Storage). Simple polling via `useEffect` + `setInterval` at 5 s.

**Rationale**: Vite is the standard React build tool (Create React App is
deprecated). Uploading through the backend avoids exposing SAS tokens to
the client and simplifies the architecture per Principle IV. A 5-second
polling interval is the simplest approach that satisfies FR-017.

**Alternatives Considered**:
- Direct-to-blob upload with SAS tokens: Rejected—requires a separate
  endpoint to generate SAS tokens, exposes storage details to the client,
  and adds complexity without demonstrating a meaningful pattern.
- WebSocket/Server-Sent Events for real-time status: Rejected—adds
  infrastructure complexity (SignalR or equivalent); polling is explicitly
  specified in the requirements (FR-017).
- Next.js or Remix: Rejected—SSR is unnecessary for a single-page upload
  tool; Vite + React is simpler (Principle IV).

**Key Technical Details**:
- Dependencies: `react`, `react-dom`, `react-dropzone`, `typescript`
- No state management library (React state + hooks sufficient for this scope)
- No CSS framework mandated—plain CSS or minimal utility classes
- `staticwebapp.config.json` for routing fallback and API proxy config
- Build output: `dist/` directory for Static Web Apps deployment

---

## R-005: Blob Storage Organization

**Decision**: Per-session container organization using a single storage
account with session-scoped virtual directories (prefixes) in shared
`source-documents` and `translated-documents` containers.

**Rationale**: Azure Document Translation batch API requires source and
target container SAS URLs. Using virtual directories within shared
containers avoids creating/deleting containers per session (which has
rate limits and is slower). The session ID prefix provides logical
isolation.

**Alternatives Considered**:
- Per-session containers: Rejected—container creation has rate limits,
  cleanup is harder, and it doesn't demonstrate the virtual directory
  pattern which is more applicable to production scenarios.
- Single flat container: Rejected—no session isolation; files would
  collide on name conflicts across sessions.

**Key Technical Details**:
- Container layout:
  ```
  source-documents/
  └── {sessionId}/
      ├── file1.docx
      └── file2.pdf
  
  translated-documents/
  └── {sessionId}/
      ├── file1.docx
      └── file2.pdf
  ```
- SAS tokens generated server-side with container-level read/write
  permissions, scoped per session prefix where supported
- Storage account also hosts Azure Functions runtime storage (separate
  from document containers)

---

## R-006: CI/CD Pipeline Strategy

**Decision**: GitHub Actions with three workflows: (1) PR validation
(build + test + lint), (2) Bicep validation (lint + what-if on infra/
changes), (3) Dependabot for dependency scanning. No CD pipeline for
deployment (azd handles deployment).

**Rationale**: The constitution requires CI gates on every PR (Principle III)
and Bicep validation (FR-028). Keeping CI and CD separate aligns with the
reference architecture's educational purpose—CI demonstrates quality gates,
while `azd up` demonstrates deployment. Adding a full CD pipeline would
add complexity without teaching a new pattern (Principle IV).

**Alternatives Considered**:
- Azure DevOps Pipelines: Rejected—GitHub Actions is already present
  in the `.github/` directory structure and is more accessible to the
  open-source audience.
- Combined CI/CD pipeline: Rejected—conflates validation with deployment;
  separating them is clearer for a reference architecture.
- Full CD with staging environments: Rejected—production concern that
  doesn't demonstrate the core translation patterns (Principle IV).

**Key Technical Details**:
- PR validation workflow: `dotnet build`, `dotnet test`, `npm run build`,
  `npm run lint`, `npm test`
- Bicep validation workflow: `az bicep lint`, conditional on `infra/**`
  path changes
- Dependabot: configured for NuGet and npm ecosystems
- All workflows use `on: pull_request` trigger with `main` branch target

---

## R-007: Observability Approach

**Decision**: Azure Functions built-in Application Insights integration.
No custom dashboards, no distributed tracing infrastructure, no custom
metrics. Structured logging via `ILogger` with key orchestration events.

**Rationale**: NFR-001 and NFR-002 explicitly scope observability to
"basic structured logging via Application Insights" and state "no custom
metrics dashboards or distributed tracing infrastructure are required."
The built-in integration requires only a connection string in app settings.

**Alternatives Considered**:
- OpenTelemetry with custom exporters: Rejected—NFR-001 explicitly
  excludes custom tracing infrastructure.
- Custom Application Insights dashboards: Rejected—NFR-001 explicitly
  excludes custom metrics dashboards.
- No observability: Rejected—NFR-002 requires logging key orchestration
  lifecycle events.

**Key Technical Details**:
- `APPLICATIONINSIGHTS_CONNECTION_STRING` app setting auto-enables integration
- Log events: session created, batch started, batch completed/failed,
  session completed/failed
- Durable Functions automatically logs orchestration events to App Insights
- No additional NuGet packages needed beyond the Functions Worker SDK
