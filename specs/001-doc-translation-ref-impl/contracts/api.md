# API Contract: Document Translation Backend

**Date**: 2025-07-17
**Feature Branch**: `001-doc-translation-ref-impl`

## Overview

The backend exposes a REST API via Azure Functions HTTP triggers. The
frontend communicates exclusively through these endpoints. All responses
use JSON. No authentication is required (per spec assumption: no auth
for reference implementation).

The backend Azure Function App base URL is configured in the frontend
via the Static Web App's linked backend feature (requests to `/api/*`
are proxied automatically).

---

## Endpoints

### POST /api/translate

Start a new translation session. Accepts multipart file upload with
target language specification.

**Request**:
```
POST /api/translate
Content-Type: multipart/form-data

Parts:
  - targetLanguage: string (form field, ISO 639-1 code, e.g., "es")
  - files: File[] (one or more file parts)
```

**Success Response** (202 Accepted):
```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Uploading",
  "statusUrl": "/api/translate/a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "createdAt": "2025-07-17T10:30:00Z"
}
```

**Validation Error Response** (400 Bad Request):
```json
{
  "error": "No files provided. Please select at least one file to translate.",
  "details": []
}
```

```json
{
  "error": "Some files could not be accepted.",
  "details": [
    "report.exe: Unsupported file type. Supported types: PDF, DOCX, XLSX, PPTX, HTML, TXT.",
    "huge-file.pdf: File too large. Maximum file size is 100 MB."
  ]
}
```

**Validation Rules**:
- At least one file required.
- `targetLanguage` required, must be a supported language code.
- Each file must have a supported extension.
- Each file must be ≤ 100 MB.
- Unsupported or oversized files are rejected; valid files in the same
  request are NOT partially accepted (all-or-nothing validation).

---

### GET /api/translate/{sessionId}

Get the current status of a translation session. This is the endpoint
the frontend polls at 5-second intervals.

**Request**:
```
GET /api/translate/{sessionId}
```

**Response** (200 OK — session in progress):
```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Processing",
  "targetLanguage": "es",
  "totalFiles": 1500,
  "createdAt": "2025-07-17T10:30:00Z",
  "batches": [
    {
      "batchId": "batch-001",
      "status": "Running",
      "fileCount": 1000
    },
    {
      "batchId": "batch-002",
      "status": "Pending",
      "fileCount": 500
    }
  ]
}
```

**Response** (200 OK — session completed):
```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Completed",
  "targetLanguage": "es",
  "totalFiles": 1500,
  "createdAt": "2025-07-17T10:30:00Z",
  "batches": [
    {
      "batchId": "batch-001",
      "status": "Succeeded",
      "fileCount": 1000,
      "translatedFileCount": 1000
    },
    {
      "batchId": "batch-002",
      "status": "Succeeded",
      "fileCount": 500,
      "translatedFileCount": 500
    }
  ],
  "downloadUrl": "/api/translate/a1b2c3d4-e5f6-7890-abcd-ef1234567890/download"
}
```

**Response** (200 OK — session failed with partial results):
```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Failed",
  "targetLanguage": "es",
  "totalFiles": 1500,
  "createdAt": "2025-07-17T10:30:00Z",
  "batches": [
    {
      "batchId": "batch-001",
      "status": "Succeeded",
      "fileCount": 1000,
      "translatedFileCount": 1000
    },
    {
      "batchId": "batch-002",
      "status": "Failed",
      "fileCount": 500,
      "error": "Translation service returned an error for this batch."
    }
  ],
  "error": "Translation partially failed. 1 of 2 batches failed.",
  "downloadUrl": "/api/translate/a1b2c3d4-e5f6-7890-abcd-ef1234567890/download"
}
```

**Not Found Response** (404 Not Found):
```json
{
  "error": "Translation session not found."
}
```

---

### GET /api/translate/{sessionId}/download

Download translated files for a completed (or partially completed)
session. Returns a zip archive containing all successfully translated
files.

**Request**:
```
GET /api/translate/{sessionId}/download
```

**Success Response** (200 OK):
```
Content-Type: application/zip
Content-Disposition: attachment; filename="translated-{sessionId}.zip"

[binary zip content]
```

**Not Ready Response** (409 Conflict):
```json
{
  "error": "Translation is still in progress. Please wait for completion."
}
```

**No Files Response** (404 Not Found):
```json
{
  "error": "No translated files available for this session."
}
```

---

### GET /api/languages

Get the list of supported target languages. Proxies the Azure Translator
supported languages API and returns a simplified list.

**Request**:
```
GET /api/languages
```

**Response** (200 OK):
```json
{
  "languages": [
    { "code": "es", "name": "Spanish" },
    { "code": "fr", "name": "French" },
    { "code": "de", "name": "German" },
    { "code": "ja", "name": "Japanese" },
    { "code": "zh-Hans", "name": "Chinese (Simplified)" }
  ]
}
```

---

## Status Values

### Session Status

| Value | Description | Terminal? |
|-------|-------------|-----------|
| `Uploading` | Files being uploaded to blob storage | No |
| `Processing` | Orchestration running, batches in progress | No |
| `Completed` | All batches succeeded | Yes |
| `Failed` | One or more batches failed | Yes |
| `Error` | Orchestration-level unhandled error | Yes |

### Batch Status

| Value | Description | Terminal? |
|-------|-------------|-----------|
| `Pending` | Created, not yet submitted | No |
| `Submitted` | Submitted to translation API | No |
| `Running` | Translation in progress | No |
| `Succeeded` | All files translated | Yes |
| `PartiallySucceeded` | Some files translated, some failed | Yes |
| `Failed` | Batch failed | Yes |

---

## Error Response Format

All error responses use this structure:

```json
{
  "error": "Human-readable error message.",
  "details": ["Optional array of specific issues."]
}
```

- `error` is always present with a human-readable message (FR-029, FR-030).
- `details` is optional; present when multiple specific issues exist
  (e.g., multiple file validation failures).
- No error codes are used (per spec clarification).

---

## Frontend Polling Contract

The frontend polls `GET /api/translate/{sessionId}` at a fixed 5-second
interval (FR-017). Polling behavior:

1. Start polling immediately after receiving the `202 Accepted` response
   from `POST /api/translate`.
2. Use the `sessionId` from the response.
3. Poll every 5 seconds.
4. Stop polling when `status` is a terminal value (`Completed`, `Failed`,
   or `Error`) (FR-019).
5. When `status` is `Completed` or `Failed` and `downloadUrl` is present,
   enable the download button (FR-015).

---

## Static Web App Routing

The Static Web App proxies API requests to the linked Function App:

```json
// staticwebapp.config.json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/api/*"]
  }
}
```

All `/api/*` requests are routed to the linked Azure Function App backend
automatically by the Static Web App platform. No CORS configuration is
required.
