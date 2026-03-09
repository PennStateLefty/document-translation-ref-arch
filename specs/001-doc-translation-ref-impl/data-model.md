# Data Model: Document Translation Reference Implementation

**Date**: 2025-07-17
**Feature Branch**: `001-doc-translation-ref-impl`

## Overview

This reference implementation does not use a traditional database. All state
is managed through two mechanisms:

1. **Durable Functions orchestration state** — persisted automatically by the
   Durable Task Framework in Azure Storage (table + blob). This is the source
   of truth for session and batch status.
2. **Azure Blob Storage** — source and translated document files organized by
   session ID.

The entities below describe the logical data model as represented in C#
classes (used as orchestration inputs/outputs and activity function
parameters) and in blob storage layout.

---

## Entities

### TranslationSession

The top-level user-facing unit of work. Created when a user uploads files
and initiates translation. Maps 1:1 with a Durable Functions orchestration
instance.

| Field | Type | Description |
|-------|------|-------------|
| `SessionId` | `string` | Unique identifier (GUID). Also the Durable Functions instance ID. |
| `TargetLanguage` | `string` | ISO 639-1 language code (e.g., `"es"`, `"fr"`, `"de"`). |
| `Status` | `TranslationStatus` | Current session status (see state machine below). |
| `CreatedAt` | `DateTimeOffset` | UTC timestamp when the session was created. |
| `TotalFileCount` | `int` | Total number of source files in the session. |
| `TotalFileSize` | `long` | Total size of source files in bytes. |
| `Batches` | `List<TranslationBatch>` | Batches created after splitting. Populated by the orchestrator. |
| `Error` | `string?` | Error message if session failed. Null otherwise. |

**Validation Rules**:
- `SessionId`: Non-empty, valid GUID format.
- `TargetLanguage`: Non-empty, must be a supported language code.
- `TotalFileCount`: ≥ 1.
- `TotalFileSize`: > 0, each individual file ≤ 100 MB.

---

### TranslationBatch

A subset of files within a session that is submitted as a single request to
the Azure Document Translation batch API. Created by the batch splitting
logic to respect service limits.

| Field | Type | Description |
|-------|------|-------------|
| `BatchId` | `string` | Unique identifier (GUID) for this batch. |
| `SessionId` | `string` | Parent session identifier. |
| `FileCount` | `int` | Number of files in this batch. |
| `TotalSize` | `long` | Total size of files in this batch (bytes). |
| `Status` | `BatchStatus` | Current batch status. |
| `TranslationOperationId` | `string?` | Operation ID returned by the Document Translation API. |
| `SourceBlobPrefix` | `string` | Blob prefix for source files: `{sessionId}/{batchId}/`. |
| `TargetBlobPrefix` | `string` | Blob prefix for translated files: `{sessionId}/{batchId}/`. |
| `Error` | `string?` | Error message if batch failed. Null otherwise. |

**Validation Rules**:
- `FileCount`: 1–1,000 (service limit).
- `TotalSize`: ≤ 250 MB (268,435,456 bytes, service limit).
- `SourceBlobPrefix`: Non-empty, follows `{sessionId}/{batchId}/` pattern.

**Batch Splitting Rules** (FR-009, FR-010):
- If session has > 1,000 files → split into batches of ≤ 1,000 files each.
- If session total size > 250 MB → split into batches of ≤ 250 MB each.
- Both limits evaluated simultaneously; the more restrictive limit determines the split points.
- A single file > 250 MB is rejected at upload validation (FR-004).
- Exactly 1,000 files or exactly 250 MB → single batch, no split (edge case from spec).

---

### SourceDocument

Metadata for an individual file uploaded by the user. Tracked as part of
the upload flow but not persisted to a database—exists as blob metadata
and as part of orchestration input.

| Field | Type | Description |
|-------|------|-------------|
| `FileName` | `string` | Original file name as uploaded by the user. |
| `FileSize` | `long` | File size in bytes. |
| `ContentType` | `string` | MIME type (e.g., `application/pdf`). |
| `BlobUrl` | `string` | Full blob storage URL after upload. |

**Validation Rules**:
- `FileName`: Non-empty, must have a supported file extension.
- `FileSize`: > 0 and ≤ 100 MB (104,857,600 bytes).
- Supported extensions: `.pdf`, `.docx`, `.xlsx`, `.pptx`, `.html`, `.htm`, `.txt`, `.xlf`, `.xliff`, `.tsv`.

---

### TranslationResult

Output of a completed (or failed) batch translation, returned by the
activity function that monitors the Document Translation API.

| Field | Type | Description |
|-------|------|-------------|
| `BatchId` | `string` | The batch this result corresponds to. |
| `Status` | `BatchStatus` | Terminal status: `Succeeded`, `Failed`, or `PartiallySucceeded`. |
| `TranslatedFileCount` | `int` | Number of files successfully translated. |
| `FailedFileCount` | `int` | Number of files that failed translation. |
| `Error` | `string?` | Error details if the batch failed entirely. |

---

## Enumerations

### TranslationStatus (Session-level)

```csharp
public enum TranslationStatus
{
    Uploading,      // Files being uploaded to blob storage
    Processing,     // Orchestration running (batches in progress)
    Completed,      // All batches succeeded
    Failed,         // One or more batches failed (partial failure)
    Error           // Orchestration-level error (e.g., unhandled exception)
}
```

### BatchStatus (Batch-level)

```csharp
public enum BatchStatus
{
    Pending,              // Created, not yet submitted to translation API
    Submitted,            // Submitted to translation API, awaiting start
    Running,              // Translation in progress
    Succeeded,            // All files translated successfully
    PartiallySucceeded,   // Some files translated, some failed
    Failed,               // Batch translation failed
    Cancelled             // Batch was cancelled
}
```

---

## State Machine: Translation Session

```
                    ┌──────────┐
    (user uploads)  │Uploading │
         ──────────►│          │
                    └────┬─────┘
                         │ (files stored, orchestration starts)
                         ▼
                    ┌──────────┐
                    │Processing│
                    │          │◄──── (batches fan-out)
                    └────┬─────┘
                         │
              ┌──────────┼──────────┐
              ▼          ▼          ▼
         ┌─────────┐ ┌───────┐ ┌─────┐
         │Completed│ │Failed │ │Error│
         │         │ │       │ │     │
         └─────────┘ └───────┘ └─────┘
```

**Transition Rules**:
- `Uploading` → `Processing`: All files uploaded to blob, orchestrator begins batch splitting.
- `Processing` → `Completed`: All batches report `Succeeded`.
- `Processing` → `Failed`: At least one batch reports `Failed` or `PartiallySucceeded` (FR-020).
- `Processing` → `Error`: Unhandled exception in orchestrator.
- Terminal states: `Completed`, `Failed`, `Error` — no further transitions.

---

## State Machine: Translation Batch

```
    ┌───────┐    ┌─────────┐    ┌───────┐
    │Pending├───►│Submitted├───►│Running│
    └───────┘    └─────────┘    └───┬───┘
                                    │
                    ┌───────────────┬┴──────────────┐
                    ▼               ▼               ▼
              ┌─────────┐   ┌───────────────┐  ┌──────┐
              │Succeeded│   │PartiallySucc. │  │Failed│
              └─────────┘   └───────────────┘  └──────┘
```

---

## Blob Storage Layout

```
Storage Account
├── source-documents (container)
│   └── {sessionId}/
│       ├── file1.docx
│       ├── file2.pdf
│       └── ...
│
├── translated-documents (container)
│   └── {sessionId}/
│       ├── file1.docx
│       ├── file2.pdf
│       └── ...
│
└── azure-webjobs-* (containers - Durable Functions internal storage)
```

**Notes**:
- Source documents uploaded with original file names under session prefix.
- Translated documents written by the Document Translation service to the
  target container under the same session prefix, preserving file names.
- Durable Functions uses its own internal containers/tables for orchestration
  state—these are not application-managed.
- For multi-batch sessions, the batch splitting is logical (files are not
  physically reorganized); the batch definition references file lists that
  the activity function uses when constructing the API request.

---

## Relationships

```
TranslationSession (1) ──────── (1..*) TranslationBatch
        │                                     │
        │                                     │
   (1..*) SourceDocument              (1) TranslationResult
```

- A `TranslationSession` contains one or more `TranslationBatch` records.
- A `TranslationSession` references one or more `SourceDocument` records
  (via blob prefix).
- Each `TranslationBatch` produces exactly one `TranslationResult` when it
  reaches a terminal state.
- `SourceDocument` → `TranslatedDocument` mapping is implicit: same file
  name in the target container under the same session prefix.
