# Known Limitations

> **This is a reference implementation designed to demonstrate the [Durable Functions fan-out/fan-in scalability pattern](scalability-pattern.md). It is NOT intended as a production system.** The choices below were made to keep the implementation focused on the core pattern while providing a working end-to-end demo. Each section describes the limitation and its implications.

## Static Web Apps as the Frontend Host

Azure Static Web Apps (SWA) is used to host the React SPA and proxy API requests to the Azure Functions backend via the [linked backend](https://learn.microsoft.com/azure/static-web-apps/functions-bring-your-own) feature. While convenient for demos, this introduces several constraints:

- **Reverse proxy overhead** — All `/api/*` requests route through SWA's reverse proxy before reaching the Function App. This adds latency to every API call and imposes SWA's own request size limits on file uploads, which may be lower than what the Function App itself would accept.
- **No WebSocket or Server-Sent Events support** — SWA's proxy does not support persistent connections, so the frontend must poll for translation status on a 5-second interval rather than receiving real-time push updates. This adds unnecessary HTTP traffic and up to 5 seconds of latency before status changes are visible.
- **No authentication configured** — All API endpoints use `AuthorizationLevel.Anonymous`. SWA supports built-in authentication providers (Entra ID, GitHub, etc.) but none are configured in this implementation. Any user with the URL can upload files, check status, and download results.
- **Constrained routing and scaling** — SWA's linked backend model ties the API lifecycle to the SWA deployment. In production, an API Management instance or direct Function App exposure with Azure Front Door would provide more flexibility for routing, throttling, versioning, and independent scaling.

## In-Memory File Upload Handling

The `TranslateHttpTrigger` function handles multipart file uploads entirely in memory:

1. The complete HTTP request body is read into a `byte[]` via `ReadRequestBodyAsync()` before any processing begins.
2. A custom manual multipart boundary parser extracts individual files from the byte array.
3. Each extracted file is then streamed to Azure Blob Storage.

**Implications:**

- All uploaded files are held simultaneously in the Function App's memory. With the instance memory configured at **2,048 MB** and individual files up to 30 MB each, a single upload of ~60 files could exhaust available memory — well below the 1,000-file batch limit.
- The custom multipart parser, while functional, does not benefit from ASP.NET Core's built-in multipart model binding, which supports streaming request bodies to disk or directly to external storage without buffering the full payload.
- There is no chunked or resumable upload support. If the connection drops mid-upload, the entire upload must restart.

## In-Memory Zip File Creation and Download

The `DownloadHttpTrigger` function creates a zip archive of all translated documents entirely in memory:

1. All translated blobs for the session are listed from `translated-documents/{sessionId}/`.
2. Each blob is downloaded into a separate `MemoryStream`.
3. A `ZipArchive` is created in yet another `MemoryStream`, and all downloaded blobs are added as entries.
4. The completed zip is returned as the HTTP response body.

**Implications:**

- For a session with thousands of translated documents, the Function App must hold **every translated file plus the zip archive** in memory simultaneously. This will exceed the 2,048 MB instance memory limit for any non-trivial session.
- There is no streaming zip generation — the entire archive must be built before the first byte of the response is sent.
- There is no support for resumable downloads. If the connection drops, the client must restart the entire download.
- There is no way to download individual files — only the full zip archive.
- The zip is generated on-demand for every download request rather than being pre-built once after translation completes.

## Polling-Based Status Updates

The frontend polls `GET /api/translate/{sessionId}` every 5 seconds to check translation progress. This is a deliberate simplification:

- **Added latency** — Status changes can take up to 5 seconds to appear in the UI.
- **Unnecessary HTTP traffic** — Every active session generates a request every 5 seconds, regardless of whether anything has changed. At scale with many concurrent users, this creates significant load on both the SWA proxy and the Function App.
- **No push notifications** — A production system would use WebSocket connections or Server-Sent Events (SSE) to push status updates to the client in real time.

## No Persistent Queryable State

Session state exists only within the Durable Functions task hub (Azure Table and Blob Storage). There is no external relational or document database for:

- Querying historical translation sessions
- Generating usage reports or analytics
- Supporting administrative views across all sessions
- Searching or filtering past translations

Once the Durable Functions task hub is purged (or the storage account is deleted), all session history is lost.

## No Rate Limiting or Abuse Protection

API endpoints have no throttling, rate limiting, or IP-based access controls. In this reference implementation, any client can:

- Submit unlimited translation requests
- Upload arbitrarily many files (within the in-memory constraint)
- Poll status as frequently as desired
- Download results without authentication

## Summary

| Area | Limitation | Production Impact |
|------|-----------|-------------------|
| **Static Web Apps** | Proxy overhead, no WebSocket, no auth | Latency, polling required, no access control |
| **File Upload** | Entire request buffered in memory | Memory exhaustion with large/many files |
| **Zip Download** | All blobs + archive held in memory | Memory exhaustion, no resumable download |
| **Status Updates** | 5-second polling | Latency, unnecessary traffic at scale |
| **State Persistence** | Durable Functions task hub only | No queryable history, no analytics |
| **Security** | No auth, no rate limiting | Open access, abuse risk |

For guidance on addressing these limitations, see the [Production Readiness Guide](production-readiness.md).
