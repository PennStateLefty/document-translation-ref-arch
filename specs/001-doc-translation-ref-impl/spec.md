# Feature Specification: Document Translation Reference Implementation

**Feature Branch**: `001-doc-translation-ref-impl`  
**Created**: 2025-07-17  
**Status**: Draft  
**Input**: User description: "Document Translation Reference Architecture - React frontend with drag-and-drop upload, Azure Functions backend with Durable Functions orchestration, Azure Document Translation integration, Bicep IaC, azd support, CI/CD pipelines, and polling status mechanism"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload Documents and Start Translation (Priority: P1)

An audit professional receives a tranche of documents in one or more foreign languages. They open the translation application in their browser, drag and drop the document files onto the upload area (or use a file picker), select the target language, and initiate a translation session. The system accepts the files, creates a translation session, and begins processing. The user sees immediate confirmation that their upload was accepted and translation has started.

**Why this priority**: This is the core value proposition of the entire reference architecture. Without the ability to upload files and trigger translation, no other feature has meaning. This story exercises the full vertical slice: frontend upload, storage ingestion, and backend orchestration kickoff.

**Independent Test**: Can be fully tested by uploading one or more files through the UI and confirming that the files are stored and the translation orchestration is initiated. Delivers the fundamental value of accepting documents for translation.

**Acceptance Scenarios**:

1. **Given** the user has opened the application in a browser, **When** they drag and drop one or more supported document files onto the upload area and select a target language, **Then** the system uploads all files, creates a translation session, and displays a confirmation with the session identifier.
2. **Given** the user has selected files via the file picker, **When** they confirm the upload, **Then** the system behaves identically to the drag-and-drop flow, uploading files and initiating the translation session.
3. **Given** the user uploads files that include unsupported file types, **When** the upload is submitted, **Then** the system rejects the unsupported files with a clear error message and does not start a translation session for those files.
4. **Given** the user uploads an empty selection (no files), **When** they attempt to start translation, **Then** the system displays a validation message requesting at least one file.

---

### User Story 2 - Monitor Translation Progress (Priority: P2)

After starting a translation session, the audit professional wants to know the current status of their job without leaving the application or manually refreshing. The application automatically polls for status updates and displays progress information—such as "in progress," "completed," or "failed"—so the user can continue other work and check back when translation is done.

**Why this priority**: Status visibility is essential for user confidence and workflow integration. Audit professionals need to know when their documents are ready so they can proceed with their review. Without status feedback, the upload feature alone leaves users in the dark.

**Independent Test**: Can be tested by starting a translation session and observing that the UI updates automatically to reflect the current job status (in progress → completed or failed) without manual page refresh.

**Acceptance Scenarios**:

1. **Given** a translation session has been started, **When** the user views the session status, **Then** the application displays the current status (e.g., "in progress," "completed," "failed") and updates automatically via polling.
2. **Given** a translation session is in progress, **When** the user remains on the page, **Then** the status updates at regular intervals without requiring manual interaction.
3. **Given** a translation session has completed, **When** the polling detects the completed status, **Then** the UI displays a "completed" indicator, stops further polling for that session, and presents a download button/link for the translated files.
4. **Given** a translation session has failed, **When** the polling detects the failure, **Then** the UI displays an error state with a meaningful message about the failure.

---

### User Story 3 - Automatic Batch Splitting for Large Uploads (Priority: P3)

An audit professional uploads a large tranche of documents—potentially exceeding 1,000 files or 250 MB in total size. The system automatically assesses the upload and, if service limits would be exceeded, splits the work into multiple batches. Each batch is processed in parallel, and the user sees the combined result as a single translation session. The user does not need to know about or manage batch splitting.

**Why this priority**: This is critical for the scalability story of the reference architecture. Audit professionals routinely receive large document sets, and the system must handle these transparently. However, it builds on the foundation of upload (P1) and status tracking (P2).

**Independent Test**: Can be tested by uploading a set of files that exceeds the batch size limits and confirming that the system automatically creates multiple batches, processes them in parallel, and reports a unified result.

**Acceptance Scenarios**:

1. **Given** the user uploads more than 1,000 files in a single session, **When** translation is initiated, **Then** the system automatically splits the files into multiple batches of 1,000 or fewer files each and processes all batches.
2. **Given** the user uploads files totaling more than 250 MB, **When** translation is initiated, **Then** the system automatically splits the files into batches that each remain under 250 MB and processes all batches.
3. **Given** the upload requires multiple batches, **When** all batches complete, **Then** the user sees a single unified "completed" status for their translation session, not individual batch results.
4. **Given** one batch within a multi-batch session fails, **When** the failure is detected, **Then** the system reports the failure with details about which documents were affected while allowing successful batches to remain available.

---

### User Story 4 - Provision and Tear Down the Environment (Priority: P4)

A solution architect or developer evaluating this reference architecture wants to stand up the complete environment from scratch using a single command, and tear it down just as easily. They run a single provisioning command that creates all required cloud resources and deploys all application components. When they are finished evaluating, they run a single teardown command that removes everything cleanly.

**Why this priority**: This story is essential for the reference architecture's accessibility and aligns with the Azure Developer CLI Native principle. Evaluators will not adopt a reference architecture they cannot easily deploy and remove. However, it is prioritized below the core application stories because the application logic must exist before deployment tooling is meaningful.

**Independent Test**: Can be tested by running the provisioning command from a clean state and confirming all resources are created and the application is accessible, then running the teardown command and confirming all resources are removed.

**Acceptance Scenarios**:

1. **Given** a developer has the repository cloned and prerequisites installed, **When** they run the provisioning command, **Then** all cloud infrastructure is created and all application components are deployed and accessible.
2. **Given** a fully provisioned environment, **When** the developer runs the teardown command, **Then** all provisioned cloud resources are removed cleanly.
3. **Given** a partially failed provisioning attempt, **When** the developer re-runs the provisioning command, **Then** the system recovers and completes provisioning without manual intervention.

---

### User Story 5 - Validate Changes via CI/CD (Priority: P5)

A contributor submits a pull request with changes to the reference implementation. Automated pipelines run build validation, tests, linting, and dependency security scanning. The contributor and reviewers see clear pass/fail results before the PR can be merged.

**Why this priority**: CI/CD is a core principle of the constitution and is part of the pattern being demonstrated. However, it supports the development workflow rather than the end-user experience, so it is prioritized after the core application and deployment stories.

**Independent Test**: Can be tested by submitting a PR and confirming that CI gates run automatically, report results, and block merge on failure.

**Acceptance Scenarios**:

1. **Given** a contributor opens a pull request, **When** the PR is created, **Then** CI pipelines automatically run build validation, tests, and linting.
2. **Given** CI pipelines have completed, **When** all checks pass, **Then** the PR is eligible for merge.
3. **Given** CI pipelines have completed, **When** any check fails, **Then** the PR is blocked from merge and the contributor sees clear error details.
4. **Given** the repository has dependency security scanning enabled, **When** a new vulnerability is detected, **Then** an alert is created for triage.

---

### Edge Cases

- What happens when a user uploads a file with a supported extension but corrupted content? The system should pass it to the translation service and surface any resulting error clearly.
- What happens when the translation service endpoint is unavailable or returns a transient error? The orchestration should retry with appropriate backoff.
- What happens when files in a batch have mixed source languages? The translation service handles language auto-detection; the system should not require the user to specify the source language.
- What happens when a translation session is started but the user closes the browser? The backend orchestration continues processing independently, and the user can check status when they return.
- What happens when the total upload consists of exactly 1,000 files or exactly 250 MB? The system should process this as a single batch without splitting.
- What happens when a single file exceeds 250 MB? The system should reject this file with a clear error since it cannot fit in any batch.

## Requirements *(mandatory)*

### Functional Requirements

#### File Upload & Session Creation

- **FR-001**: The system MUST provide a drag-and-drop interface for uploading multiple document files simultaneously.
- **FR-002**: The system MUST also provide a standard file picker as an alternative to drag-and-drop.
- **FR-003**: The system MUST support uploading common document formats (PDF, DOCX, XLSX, PPTX, HTML, plain text, and other formats supported by the Azure Document Translation service).
- **FR-004**: The system MUST validate uploaded files before initiating translation—rejecting empty uploads, files exceeding individual size limits, and unsupported file types—with simple, human-readable error messages displayed directly in the UI.
- **FR-005**: The system MUST group all files from a single upload action into one "translation session" identified by a unique session identifier.
- **FR-006**: The system MUST allow the user to select a target language for translation from the set of languages supported by the translation service.
- **FR-007**: The system MUST store uploaded source files in cloud storage before initiating translation.

#### Translation Orchestration

- **FR-008**: The system MUST use a durable orchestration pattern to manage long-running translation jobs with checkpoint and retry semantics.
- **FR-009**: The system MUST assess each translation session to determine whether the files need to be split into multiple batches based on service limits (1,000 files per batch, 250 MB per batch).
- **FR-010**: The system MUST automatically split oversized sessions into multiple batches without user intervention.
- **FR-011**: The system MUST process multiple batches in parallel using a fan-out/fan-in orchestration pattern.
- **FR-012**: The system MUST support configuring multiple translation service endpoints to enable throughput scaling across batches.
- **FR-013**: The system MUST invoke the translation service's batch API to translate documents while preserving the original document formatting.
- **FR-014**: The system MUST store translated output files in cloud storage, organized by translation session.
- **FR-015**: The frontend MUST provide a download button/link for each completed translation session, allowing the user to download the translated files directly from the UI.

#### Status & Polling

- **FR-016**: The system MUST expose the orchestration status via the orchestration identifier so the frontend can poll for updates.
- **FR-017**: The frontend MUST poll the backend at a 5-second interval to retrieve the current translation session status.
- **FR-018**: The system MUST report translation session status as one of: "uploading," "in progress," "completed," or "failed."
- **FR-019**: The system MUST stop polling automatically once a terminal status ("completed" or "failed") is reached.
- **FR-020**: When a multi-batch session has partial failures, the system MUST report which batches failed and which succeeded.

#### Infrastructure & Deployment

- **FR-021**: All cloud resources MUST be defined as Infrastructure-as-Code in Bicep templates under the `infra/` directory.
- **FR-022**: The system MUST be fully provisionable and deployable via a single Azure Developer CLI command from a clean state.
- **FR-023**: The system MUST be fully teardownable via a single Azure Developer CLI command, removing all provisioned resources.
- **FR-024**: The repository MUST include an `azure.yaml` manifest at the root as the single source of truth for service-to-infrastructure mappings.

#### CI/CD & Quality

- **FR-025**: The repository MUST include PR validation pipelines that run build, test, and lint checks on every pull request.
- **FR-026**: The repository MUST have dependency security scanning enabled with automated alerting.
- **FR-027**: The system MUST include automated tests covering critical orchestration paths—specifically batch splitting logic and the fan-out/fan-in pattern.
- **FR-028**: All Bicep templates MUST pass linting and validation as part of CI checks when infrastructure files are modified.

#### Error Handling & User Feedback

- **FR-029**: The system MUST display simple, human-readable error messages in the UI when upload validation fails, translation fails, or service errors occur. No error code system is required.
- **FR-030**: Error messages MUST be actionable where possible (e.g., "File too large—maximum size is 250 MB" rather than "Error 413").

### Non-Functional Requirements

#### Observability

- **NFR-001**: The system MUST use structured logging via Application Insights, leveraging Azure Functions' built-in Application Insights integration. No custom metrics dashboards or distributed tracing infrastructure are required.
- **NFR-002**: Logs MUST capture key orchestration lifecycle events (session created, batch started, batch completed/failed, session completed/failed) to support basic debugging and operational visibility.

### Key Entities

- **Translation Session**: Represents a single user-initiated upload and translation request. Contains a unique identifier, target language, creation timestamp, current status, and references to one or more translation batches. A session is the user-facing unit of work.
- **Translation Batch**: A subset of files within a translation session that is submitted as a single request to the translation service. Each batch respects the service limits (≤1,000 files, ≤250 MB). Contains a batch identifier, list of source file references, status, and reference to the parent session.
- **Source Document**: An individual file uploaded by the user. Contains a file name, size, format/type, and storage location reference. Belongs to exactly one translation session and is assigned to exactly one batch.
- **Translated Document**: The output produced by the translation service for a given source document. Contains a file name, target language, storage location reference, and reference to its source document.

## Assumptions

- **Source language auto-detection**: The translation service will automatically detect the source language of each document. Users are not required to specify the source language.
- **Single target language per session**: Each translation session translates all documents to one target language. If the user needs multiple target languages, they create separate sessions.
- **No authentication for reference implementation**: As a non-production reference architecture focused on demonstrating scalable translation patterns (per Constitution Principle IV—Simplicity & Pattern Focus), the reference implementation will not include user authentication or authorization. Production adopters would add their own identity layer.
- **Monorepo structure**: Frontend, backend, and infrastructure code all live in a single repository, consistent with the constitution's technology stack requirements.
- **Retry semantics for transient failures**: The orchestration will include basic retry logic for transient translation service failures, using the durable orchestration framework's built-in retry capabilities.
- **File format preservation**: The translation service preserves the formatting and layout of the original document (e.g., a DOCX input produces a DOCX output with the same structure).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can upload a set of documents and initiate a translation session in under 2 minutes (excluding file transfer time for very large uploads).
- **SC-002**: The system correctly splits a 2,500-file upload into 3 batches and processes all batches to completion without user intervention.
- **SC-003**: Translation session status updates are visible in the UI within 10 seconds of a backend status change.
- **SC-004**: 95% of users can complete an end-to-end translation workflow (upload → monitor → access translated files) on their first attempt without documentation or guidance.
- **SC-005**: The complete environment can be provisioned from a clean state and be fully operational within 15 minutes using a single command.
- **SC-006**: The complete environment can be torn down and all resources removed using a single command.
- **SC-007**: All CI validation gates (build, test, lint, security scan) execute and report results on every pull request.
- **SC-008**: Automated tests cover batch splitting logic for sessions at, below, and above both the file count limit (1,000) and size limit (250 MB).
- **SC-009**: The system handles concurrent translation sessions without interference between sessions.

## Clarifications

### Session 2026-03-04

- Q: How do users access translated files after completion? → A: Download button/link in the UI for each completed session's translated files.
- Q: What polling interval should the frontend use for status updates? → A: 5-second polling interval (reasonable default for non-production reference implementation).
- Q: What level of observability is required? → A: Basic structured logging via Application Insights (Azure Functions built-in integration). No custom metrics or distributed tracing dashboards.
- Q: What error message format should the system use? → A: Simple, human-readable error messages displayed in the UI. No error code system.
- Q: How should remaining ambiguities be resolved? → A: Apply Simplicity & Pattern Focus principle (Principle IV)—choose the simplest reasonable default that demonstrates the architectural pattern.
