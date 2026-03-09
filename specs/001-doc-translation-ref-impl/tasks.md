# Tasks: Document Translation Reference Implementation

**Input**: Design documents from `/specs/001-doc-translation-ref-impl/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/api.md ✅, quickstart.md ✅

**Tests**: Included — FR-027 and Constitution Principle III explicitly require automated tests for batch splitting and fan-out/fan-in orchestration. Plan.md defines test files for both backend (xUnit) and frontend (vitest).

**Organization**: Tasks grouped by user story from spec.md (5 stories: P1–P5). Each story can be implemented and tested independently after foundational phase completion.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- All file paths are relative to repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create monorepo structure, initialize C# backend, React frontend, and test projects with all required dependencies.

- [X] T001 Create monorepo directory structure per plan.md: `src/api/Models/`, `src/api/Functions/`, `src/api/Services/`, `src/api/DocumentTranslation.Api.Tests/`, `src/web/src/components/`, `src/web/src/hooks/`, `src/web/src/services/`, `src/web/src/types/`, `src/web/tests/`, `infra/modules/`, `.github/workflows/`
- [X] T002 [P] Initialize C# .NET 8 Azure Functions isolated worker project with required NuGet dependencies (Microsoft.Azure.Functions.Worker, Extensions.DurableTask, Azure.AI.Translation.Document, Azure.Storage.Blobs, Azure.Identity) in `src/api/DocumentTranslation.Api.csproj`
- [X] T003 [P] Initialize React 18 + Vite + TypeScript project with dependencies (react, react-dom, react-dropzone, vitest) in `src/web/package.json`, `src/web/vite.config.ts`, and `src/web/tsconfig.json`
- [X] T004 Initialize xUnit test project referencing the API project in `src/api/DocumentTranslation.Api.Tests/DocumentTranslation.Api.Tests.csproj`
- [X] T005 [P] Create azure.yaml azd manifest mapping `api` → Function App and `web` → Static Web App at repository root `azure.yaml`
- [X] T006 [P] Create .gitignore with .NET (bin/, obj/), Node.js (node_modules/, dist/), and IDE exclusions at repository root `.gitignore`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model classes, service interfaces, DI configuration, and frontend type scaffolding that ALL user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Backend Data Model (from data-model.md)

- [X] T007 Create TranslationStatus and BatchStatus enumerations in `src/api/Models/Enums.cs`
- [X] T008 [P] Create SourceDocument model with file validation rules (supported extensions, 100 MB max) in `src/api/Models/SourceDocument.cs`
- [X] T009 [P] Create TranslationBatch model with service limit constants (1,000 files, 250 MB) in `src/api/Models/TranslationBatch.cs`
- [X] T010 [P] Create TranslationSession model with session state tracking in `src/api/Models/TranslationSession.cs`
- [X] T011 [P] Create TranslationResult model with batch outcome fields in `src/api/Models/TranslationResult.cs`

### Backend Service Interfaces

- [X] T012 [P] Create IBlobStorageService interface (upload, list, download, generate SAS) in `src/api/Services/IBlobStorageService.cs`
- [X] T013 [P] Create ITranslationService interface (start batch, check status, get languages) in `src/api/Services/ITranslationService.cs`

### Backend Configuration

- [X] T014 Configure host builder with DI registrations for BlobStorageService, TranslationService, and Azure SDK clients in `src/api/Program.cs`
- [X] T015 [P] Configure Durable Functions runtime settings, extension bundles, and structured logging in `src/api/host.json`

### Frontend Scaffolding

- [X] T016 [P] Create TypeScript interfaces matching API contract (TranslationSession, BatchInfo, LanguageOption, ErrorResponse, API response types) in `src/web/src/types/translation.ts`
- [X] T017 [P] Create HTTP API client service with methods for translate, getStatus, download, and getLanguages in `src/web/src/services/apiClient.ts`
- [X] T018 [P] Configure Static Web App navigation fallback and API proxy routing in `src/web/staticwebapp.config.json`
- [X] T019 [P] Create HTML entry point with root div mount and Vite script tag in `src/web/index.html`

**Checkpoint**: Foundation ready — all models, interfaces, DI, and frontend scaffolding in place. User story implementation can now begin.

---

## Phase 3: User Story 1 — Upload Documents and Start Translation (Priority: P1) 🎯 MVP

**Goal**: Enable a user to drag-and-drop files, select a target language, and initiate a translation session that uploads files to blob storage and starts orchestration.

**Independent Test**: Upload one or more files through the UI, confirm files are stored in blob and orchestration is initiated with a session ID returned to the user.

**Acceptance Criteria** (from spec.md):
- Drag-and-drop and file picker upload supported
- Unsupported file types rejected with clear error
- Empty selection shows validation message
- Session ID and confirmation displayed on success

### Implementation for User Story 1

- [X] T020 [US1] Implement BlobStorageService with upload-to-container, list-by-prefix, and SAS token generation using Azure.Storage.Blobs SDK in `src/api/Services/BlobStorageService.cs`
- [X] T021 [P] [US1] Implement LanguagesHttpTrigger (GET /api/languages) proxying Azure Translator supported languages API in `src/api/Functions/LanguagesHttpTrigger.cs`
- [X] T022 [US1] Implement TranslateHttpTrigger (POST /api/translate) with multipart form-data parsing, file validation (extension, size, empty check), and Durable Functions orchestration start in `src/api/Functions/TranslateHttpTrigger.cs`
- [X] T023 [US1] Implement initial TranslationOrchestrator with UploadFilesToBlob activity that stores files under `source-documents/{sessionId}/` prefix in `src/api/Functions/TranslationOrchestrator.cs`
- [X] T024 [P] [US1] Create FileUpload component with react-dropzone drag-and-drop area and file picker fallback in `src/web/src/components/FileUpload.tsx`
- [X] T025 [P] [US1] Create LanguageSelector dropdown component fetching supported languages from GET /api/languages in `src/web/src/components/LanguageSelector.tsx`
- [X] T026 [P] [US1] Create ErrorMessage component for displaying validation and API errors with human-readable messages in `src/web/src/components/ErrorMessage.tsx`
- [X] T027 [US1] Create useTranslation hook managing file selection state, upload submission via apiClient, and session creation response handling in `src/web/src/hooks/useTranslation.ts`
- [X] T028 [US1] Compose App component integrating FileUpload, LanguageSelector, ErrorMessage, and Translate button into the upload workflow in `src/web/src/App.tsx`
- [X] T029 [P] [US1] Create React application entry point rendering App into root div in `src/web/src/main.tsx`

### Tests for User Story 1

- [X] T030 [US1] Write upload validation unit tests covering: no files, unsupported extensions, oversized files (>100 MB), valid uploads, and all-or-nothing rejection in `src/api/DocumentTranslation.Api.Tests/ValidationTests.cs`
- [X] T031 [P] [US1] Write FileUpload component tests verifying drag-and-drop rendering, file acceptance, and rejection callback behavior in `src/web/tests/FileUpload.test.tsx`

**Checkpoint**: User Story 1 complete — users can upload documents, select a language, and receive a session ID. The upload flow is fully functional and testable end-to-end.

---

## Phase 4: User Story 2 — Monitor Translation Progress (Priority: P2)

**Goal**: Enable automatic 5-second polling of translation session status with live UI updates and a download button when translation completes.

**Independent Test**: Start a translation session, observe the UI automatically updating status (Processing → Completed/Failed) without manual refresh, and verify the download button appears on completion.

**Acceptance Criteria** (from spec.md):
- Status displayed and auto-updated via polling (no manual refresh)
- Polling at 5-second intervals
- Polling stops on terminal status (Completed, Failed, Error)
- Download button appears for completed/partially-failed sessions
- Error state shows meaningful failure message

### Implementation for User Story 2

- [X] T032 [US2] Implement StatusHttpTrigger (GET /api/translate/{sessionId}) querying Durable Functions orchestration status and returning session/batch detail per API contract in `src/api/Functions/StatusHttpTrigger.cs`
- [X] T033 [US2] Implement DownloadHttpTrigger (GET /api/translate/{sessionId}/download) generating a zip archive of translated files from `translated-documents/{sessionId}/` container prefix in `src/api/Functions/DownloadHttpTrigger.cs`
- [X] T034 [P] [US2] Create TranslationStatus component displaying session status, batch-level progress, and error messages with auto-updating UI in `src/web/src/components/TranslationStatus.tsx`
- [X] T035 [P] [US2] Create DownloadButton component for completed sessions triggering file download via GET /api/translate/{sessionId}/download in `src/web/src/components/DownloadButton.tsx`
- [X] T036 [US2] Create usePolling hook with 5-second setInterval, terminal state detection (Completed/Failed/Error), and automatic cleanup in `src/web/src/hooks/usePolling.ts`
- [X] T037 [US2] Integrate TranslationStatus, DownloadButton, and usePolling into App component for post-upload monitoring flow in `src/web/src/App.tsx`

### Tests for User Story 2

- [X] T038 [US2] Write usePolling hook tests verifying 5-second interval, terminal state stop, and cleanup on unmount in `src/web/tests/usePolling.test.ts`

**Checkpoint**: User Stories 1 AND 2 complete — users can upload documents, see live status updates, and download translated files. Full upload-to-download lifecycle works for single-batch sessions.

---

## Phase 5: User Story 3 — Automatic Batch Splitting for Large Uploads (Priority: P3)

**Goal**: Transparently split uploads exceeding service limits (1,000 files or 250 MB) into parallel batches using Durable Functions fan-out/fan-in, with unified status reporting.

**Independent Test**: Upload a set of files exceeding batch limits, confirm automatic splitting into multiple batches, parallel processing, and a unified session result.

**Acceptance Criteria** (from spec.md):
- >1,000 files automatically split into batches of ≤1,000
- >250 MB automatically split into batches of ≤250 MB each
- Multi-batch sessions report unified completion status
- Partial batch failure reported with affected document details
- Exactly 1,000 files / exactly 250 MB = single batch (edge case)

### Implementation for User Story 3

- [X] T039 [US3] Implement AssessAndSplitBatches activity with dual-constraint splitting (file count ≤1,000, total size ≤250 MB, simultaneous evaluation) in `src/api/Functions/TranslationOrchestrator.cs`
- [X] T040 [P] [US3] Implement TranslationService with Azure Document Translation batch API integration (start translation, poll operation status, get supported languages) using Azure.AI.Translation.Document SDK in `src/api/Services/TranslationService.cs`
- [X] T041 [US3] Implement fan-out/fan-in orchestration: fan out TranslateBatch activities across all batches, await all results, aggregate into session outcome in `src/api/Functions/TranslationOrchestrator.cs`
- [X] T042 [US3] Implement MonitorBatchTranslation activity polling Document Translation API operation status until terminal state in `src/api/Functions/TranslationOrchestrator.cs`
- [X] T043 [US3] Implement FinalizeSession activity aggregating batch results into session-level status (all succeeded → Completed, any failed → Failed) in `src/api/Functions/TranslationOrchestrator.cs`
- [X] T044 [US3] Add retry policies (exponential backoff: 3 attempts, 5s first interval, 2.0 coefficient, 5 min max) and error handling to all orchestrator activities in `src/api/Functions/TranslationOrchestrator.cs`

### Tests for User Story 3

- [X] T045 [US3] Write batch splitting unit tests covering: under limits (single batch), exactly at limits (edge), over file count limit, over size limit, both limits exceeded simultaneously, single file >250 MB rejection in `src/api/DocumentTranslation.Api.Tests/BatchSplitterTests.cs`
- [X] T046 [P] [US3] Write orchestrator unit tests covering: single-batch success, multi-batch fan-out/fan-in, partial failure aggregation, retry behavior, and session finalization in `src/api/DocumentTranslation.Api.Tests/OrchestratorTests.cs`

**Checkpoint**: User Stories 1, 2, AND 3 complete — the system handles large document sets (2,500+ files) automatically with parallel batch processing and unified status. Core application feature-complete.

---

## Phase 6: User Story 4 — Provision and Tear Down the Environment (Priority: P4)

**Goal**: Define all Azure infrastructure in Bicep modules so the entire environment is provisionable via `azd up` and teardownable via `azd down` from a clean state.

**Independent Test**: Run `azd up` from a clean state, confirm all resources are created and the application is accessible. Run `azd down` and confirm clean removal.

**Acceptance Criteria** (from spec.md):
- Single provisioning command creates all cloud resources and deploys application
- Single teardown command removes everything cleanly
- Re-running provisioning after partial failure recovers without manual intervention

### Implementation for User Story 4

- [X] T047 [P] [US4] Create Storage Account Bicep module with source-documents and translated-documents containers and blob CORS rules in `infra/modules/storage.bicep`
- [X] T048 [P] [US4] Create Azure AI Translator (CognitiveServices TextTranslation) Bicep module with managed identity access in `infra/modules/translator.bicep`
- [X] T049 [P] [US4] Create Application Insights and Log Analytics workspace Bicep module in `infra/modules/monitoring.bicep`
- [X] T050 [P] [US4] Create Azure Functions (Consumption/Flex plan) Bicep module with app settings for Storage, Translator, and App Insights connection strings in `infra/modules/function-app.bicep`
- [X] T051 [P] [US4] Create Azure Static Web App Bicep module with linked Function App backend in `infra/modules/static-web-app.bicep`
- [X] T052 [US4] Create managed identity RBAC role assignments module granting Function App identity Storage Blob Data Contributor and Cognitive Services User roles in `infra/modules/role-assignments.bicep`
- [X] T053 [US4] Create main.bicep orchestrator importing all modules with parameter passing, output exports, and resource dependencies in `infra/main.bicep`
- [X] T054 [P] [US4] Create default parameters file with environment name and location parameters in `infra/main.parameters.json`
- [X] T055 [US4] Validate azure.yaml service definitions align with Bicep outputs and test azd up/down lifecycle end-to-end

**Checkpoint**: User Story 4 complete — entire environment provisionable and teardownable via single commands. Infrastructure is fully codified.

---

## Phase 7: User Story 5 — Validate Changes via CI/CD (Priority: P5)

**Goal**: Automated PR validation pipelines for build, test, lint, Bicep validation, and dependency security scanning.

**Independent Test**: Submit a PR and confirm CI gates run automatically, report results, and block merge on failure.

**Acceptance Criteria** (from spec.md):
- CI pipelines run build, test, and lint on every PR
- Passing checks enable merge; failing checks block merge
- Bicep linting runs when infra/ files change
- Dependency security scanning with automated alerting

### Implementation for User Story 5

- [X] T056 [US5] Create PR validation workflow with jobs for: dotnet build, dotnet test, npm install, npm run build, npm run lint, npm test — triggered on pull_request to main in `.github/workflows/ci.yml`
- [X] T057 [P] [US5] Create Bicep validation workflow with az bicep lint, conditional on infra/** path changes, triggered on pull_request to main in `.github/workflows/bicep-validate.yml`
- [X] T058 [P] [US5] Configure Dependabot for NuGet and npm ecosystem scanning with weekly update schedule in `.github/dependabot.yml`

**Checkpoint**: User Story 5 complete — all PR validation gates operational. Repository is CI/CD-ready.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Observability, documentation, and final validation across all user stories.

- [X] T059 [P] Add structured ILogger logging for orchestration lifecycle events (session created, batch started, batch completed/failed, session completed/failed) across all backend functions in `src/api/Functions/`
- [X] T060 [P] Create README.md at repository root with architecture overview, pattern descriptions, prerequisites, and link to quickstart.md
- [X] T061 Code cleanup pass: consistent naming conventions, remove unused imports, verify error message quality (FR-029, FR-030) across all source files
- [X] T062 Validate end-to-end flow against quickstart.md scenarios: deploy → upload → monitor → download → teardown

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup ──────────────────────► No dependencies, start immediately
    │
    ▼
Phase 2: Foundational ──────────────► Depends on Setup; BLOCKS all user stories
    │
    ├──► Phase 3: US1 (P1) MVP ─────► Depends on Foundational only
    │        │
    │        ├──► Phase 4: US2 (P2) ► Depends on US1 (needs upload flow)
    │        │        │
    │        │        ├──► Phase 5: US3 (P3) ► Depends on US1+US2 (extends orchestrator)
    │        │        │
    │    Phase 6: US4 (P4) ─────────► Depends on Foundational only (parallel with US1-US3)
    │    Phase 7: US5 (P5) ─────────► Depends on Foundational only (parallel with US1-US4)
    │
    ▼
Phase 8: Polish ─────────────────────► Depends on all desired stories being complete
```

### User Story Dependencies

| Story | Depends On | Can Parallel With | Rationale |
|-------|-----------|-------------------|-----------|
| **US1** (P1) | Phase 2 only | US4, US5 | Core upload flow; no other story prerequisites |
| **US2** (P2) | US1 | US4, US5 | Needs upload flow to produce sessions to monitor |
| **US3** (P3) | US1, US2 | US4, US5 | Extends orchestrator and status reporting from US1/US2 |
| **US4** (P4) | Phase 2 only | US1, US2, US3, US5 | IaC is independent of application code |
| **US5** (P5) | Phase 2 only | US1, US2, US3, US4 | CI/CD is independent of application code |

### Within Each User Story

1. Backend services before HTTP triggers (services are dependencies)
2. HTTP triggers before frontend components (API must exist for UI to call)
3. Core implementation before integration tasks
4. Tests after implementation of the feature under test

### Parallel Opportunities

**Phase 1** — After T001 (directory structure):
```
T002 (C# project) ║ T003 (React project) ║ T005 (azure.yaml) ║ T006 (.gitignore)
```
Note: T004 (test project) depends on T002 (project reference)

**Phase 2** — After T007 (enums):
```
T008 (SourceDocument) ║ T009 (TranslationBatch) ║ T010 (TranslationSession) ║ T011 (TranslationResult)
T012 (IBlobStorage)   ║ T013 (ITranslation)
T015 (host.json)      ║ T016 (TS types) ║ T017 (API client) ║ T018 (SWA config) ║ T019 (index.html)
```

**Phase 3 (US1)** — Within story:
```
T020 (BlobService) ║ T021 (Languages trigger)
T024 (FileUpload)  ║ T025 (LanguageSelector) ║ T026 (ErrorMessage) ║ T029 (main.tsx)
T030 (Validation tests) ║ T031 (FileUpload tests)
```

**Phase 4 (US2)** — Within story:
```
T034 (TranslationStatus) ║ T035 (DownloadButton)
```

**Phase 5 (US3)** — Within story:
```
T039 (batch splitting) ║ T040 (TranslationService)  — different files
T045 (batch tests)     ║ T046 (orchestrator tests)  — different files
```

**Phase 6 (US4)** — All resource modules parallel:
```
T047 (storage) ║ T048 (translator) ║ T049 (monitoring) ║ T050 (function-app) ║ T051 (static-web-app)
```
Note: T052 (role-assignments) depends on T047, T048, T050. T053 (main.bicep) depends on all modules.

**Phase 7 (US5)** — All workflows parallel:
```
T057 (Bicep validate) ║ T058 (Dependabot)
```

**Cross-story parallelism** — US4 and US5 can run entirely in parallel with US1→US2→US3:
```
Developer A: US1 → US2 → US3 (application code, sequential)
Developer B: US4 (infrastructure, parallel with A)
Developer C: US5 (CI/CD, parallel with A and B)
```

---

## Parallel Example: User Story 1

```bash
# Launch parallel backend service + trigger tasks:
Task T020: "Implement BlobStorageService in src/api/Services/BlobStorageService.cs"
Task T021: "Implement LanguagesHttpTrigger in src/api/Functions/LanguagesHttpTrigger.cs"

# Launch parallel frontend component tasks:
Task T024: "Create FileUpload component in src/web/src/components/FileUpload.tsx"
Task T025: "Create LanguageSelector component in src/web/src/components/LanguageSelector.tsx"
Task T026: "Create ErrorMessage component in src/web/src/components/ErrorMessage.tsx"
Task T029: "Create main.tsx entry point in src/web/src/main.tsx"

# Launch parallel test tasks:
Task T030: "Write ValidationTests in src/api/DocumentTranslation.Api.Tests/ValidationTests.cs"
Task T031: "Write FileUpload.test.tsx in src/web/tests/FileUpload.test.tsx"
```

---

## Parallel Example: User Story 4

```bash
# Launch ALL Bicep resource modules in parallel:
Task T047: "Create storage.bicep in infra/modules/storage.bicep"
Task T048: "Create translator.bicep in infra/modules/translator.bicep"
Task T049: "Create monitoring.bicep in infra/modules/monitoring.bicep"
Task T050: "Create function-app.bicep in infra/modules/function-app.bicep"
Task T051: "Create static-web-app.bicep in infra/modules/static-web-app.bicep"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T006)
2. Complete Phase 2: Foundational (T007–T019)
3. Complete Phase 3: User Story 1 (T020–T031)
4. **STOP and VALIDATE**: Upload files via UI, confirm session creation and blob storage
5. Deploy/demo if ready — this is the minimum viable product

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test: upload + session creation → **MVP!**
3. Add US2 → Test: live status polling + download → **Core workflow complete**
4. Add US3 → Test: batch splitting with 2,500+ files → **Scalability demonstrated**
5. Add US4 → Test: `azd up` / `azd down` lifecycle → **Deployable reference architecture**
6. Add US5 → Test: PR CI gates → **Production-ready workflow**
7. Polish → Final validation against quickstart.md → **Release-ready**

### Parallel Team Strategy

With 3 developers:

1. **All together**: Complete Setup (Phase 1) + Foundational (Phase 2)
2. Once Foundational is done:
   - **Developer A**: US1 → US2 → US3 (application code, sequential chain)
   - **Developer B**: US4 (Bicep infrastructure, fully independent)
   - **Developer C**: US5 (CI/CD pipelines, fully independent)
3. **All together**: Phase 8 Polish + final validation

---

## Task Summary

| Phase | Story | Task Count | Parallel Tasks | Key Files |
|-------|-------|-----------|----------------|-----------|
| 1 — Setup | — | 6 | 4 | csproj, package.json, azure.yaml |
| 2 — Foundational | — | 13 | 11 | Models/, Services/ interfaces, types/ |
| 3 — US1 (P1) | Upload & Translate | 12 | 6 | BlobStorageService, TranslateHttpTrigger, FileUpload, App |
| 4 — US2 (P2) | Monitor Progress | 7 | 2 | StatusHttpTrigger, DownloadHttpTrigger, usePolling |
| 5 — US3 (P3) | Batch Splitting | 8 | 2 | TranslationOrchestrator, TranslationService, tests |
| 6 — US4 (P4) | Provision/Teardown | 9 | 6 | infra/modules/*.bicep, main.bicep |
| 7 — US5 (P5) | CI/CD | 3 | 2 | .github/workflows/, dependabot.yml |
| 8 — Polish | — | 4 | 2 | README.md, logging, cleanup |
| **Total** | | **62** | **35** | |

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in the same phase
- [USn] label maps each task to its user story for traceability
- Each user story is independently completable and testable at its checkpoint
- Commit after each task or logical group of parallel tasks
- Stop at any checkpoint to validate the story independently
- US4 (IaC) and US5 (CI/CD) are fully independent of application stories — ideal for parallel team members
- All error messages must be human-readable per FR-029/FR-030 — no error codes
- No authentication required (Constitution Principle IV — Simplicity & Pattern Focus)
