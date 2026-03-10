using Azure.AI.Translation.Document;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

// ── 1. Load .env ────────────────────────────────────────────────────────────
var appDir = AppContext.BaseDirectory;
// Walk up to find .env next to the .csproj (handles bin/Debug/net10.0 nesting)
var projectDir = appDir;
while (projectDir != null && !File.Exists(Path.Combine(projectDir, ".env")))
    projectDir = Directory.GetParent(projectDir)?.FullName;

if (projectDir == null)
    throw new FileNotFoundException("Could not find .env file. Make sure it exists next to LocalTesting.csproj.");

DotNetEnv.Env.Load(Path.Combine(projectDir, ".env"));

var aiServicesEndpoint = Environment.GetEnvironmentVariable("AI_SERVICES_ENDPOINT")?.TrimEnd('/')
    ?? throw new InvalidOperationException("AI_SERVICES_ENDPOINT not set in .env");
var storageAccountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME")
    ?? throw new InvalidOperationException("STORAGE_ACCOUNT_NAME not set in .env");
var sourceContainerName = Environment.GetEnvironmentVariable("SOURCE_CONTAINER") ?? "source-documents";
var targetContainerName = Environment.GetEnvironmentVariable("TARGET_CONTAINER") ?? "translated-documents";
var targetLanguage = "en";

Console.WriteLine("=== Configuration ===");
Console.WriteLine($"AI Services Endpoint : {aiServicesEndpoint}");
Console.WriteLine($"Storage Account      : {storageAccountName}");
Console.WriteLine($"Source Container      : {sourceContainerName}");
Console.WriteLine($"Target Container      : {targetContainerName}");
Console.WriteLine($"Target Language       : {targetLanguage}");
Console.WriteLine();

// ── 2. Create Azure SDK clients (mirrors Program.cs) ────────────────────────
var credential = new DefaultAzureCredential();

var blobServiceClient = new BlobServiceClient(
    new Uri($"https://{storageAccountName}.blob.core.windows.net"),
    credential);

var translationClient = new DocumentTranslationClient(
    new Uri(aiServicesEndpoint),
    credential);

Console.WriteLine("✓ BlobServiceClient created");
Console.WriteLine("✓ DocumentTranslationClient created");
Console.WriteLine();

// ── 3. Validate sample documents (mirrors TranslateHttpTrigger) ─────────────
var sampleDocsPath = Path.Combine(projectDir, "sample_docs");
if (!Directory.Exists(sampleDocsPath))
    throw new DirectoryNotFoundException($"sample_docs/ folder not found at {sampleDocsPath}");

var localFiles = Directory.GetFiles(sampleDocsPath)
    .Where(f => !Path.GetFileName(f).StartsWith('.')) // skip hidden files like .gitkeep
    .ToArray();

var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".pdf", ".docx", ".xlsx", ".pptx", ".html", ".htm", ".txt", ".xlf", ".xliff", ".tsv"
};
const long maxFileSize = 100 * 1024 * 1024;

var sessionId = Guid.NewGuid().ToString();
var sourceDocuments = new List<SourceDocumentInfo>();
var validationErrors = new List<string>();

foreach (var filePath in localFiles)
{
    var fileName = Path.GetFileName(filePath);
    var fileSize = new FileInfo(filePath).Length;
    var ext = Path.GetExtension(fileName).ToLowerInvariant();

    if (!supportedExtensions.Contains(ext))
    {
        validationErrors.Add($"{fileName}: Unsupported file type.");
        continue;
    }
    if (fileSize > maxFileSize)
    {
        validationErrors.Add($"{fileName}: File too large (max 100 MB).");
        continue;
    }

    sourceDocuments.Add(new SourceDocumentInfo(fileName, fileSize, GetContentType(fileName)));
}

if (validationErrors.Count > 0)
{
    Console.WriteLine("⚠ Validation errors:");
    foreach (var e in validationErrors) Console.WriteLine($"  {e}");
}

if (sourceDocuments.Count == 0)
    throw new InvalidOperationException("No valid files in sample_docs/. Add PDF, DOCX, XLSX, PPTX, HTML, or TXT files.");

Console.WriteLine($"Session ID : {sessionId}");
Console.WriteLine($"Files      : {sourceDocuments.Count}");
Console.WriteLine($"Total size : {sourceDocuments.Sum(d => d.FileSize):N0} bytes");
Console.WriteLine();

// ── 4. Upload to blob storage (mirrors TranslateHttpTrigger upload) ─────────
var sourceContainerClient = blobServiceClient.GetBlobContainerClient(sourceContainerName);
await sourceContainerClient.CreateIfNotExistsAsync();

foreach (var doc in sourceDocuments)
{
    var localPath = Path.Combine(sampleDocsPath, doc.FileName);
    var blobName = $"{sessionId}/{doc.FileName}";

    using var stream = File.OpenRead(localPath);
    var blobClient = sourceContainerClient.GetBlobClient(blobName);
    await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = doc.ContentType });

    Console.WriteLine($"  Uploaded: {blobName}  ({doc.FileSize:N0} bytes)");
}
Console.WriteLine("✓ All files uploaded");
Console.WriteLine();

// ── 5. Assess & split into batches (mirrors TranslationOrchestrator.SplitIntoBatches) ──
var batches = SplitIntoBatches(sourceDocuments, sessionId, targetLanguage);

Console.WriteLine($"Split into {batches.Count} batch(es):");
foreach (var batch in batches)
{
    Console.WriteLine($"  Batch {batch.BatchId[..8]}  — {batch.FileCount} files, {batch.TotalSize:N0} bytes");
    foreach (var doc in batch.Documents)
        Console.WriteLine($"    • {doc.FileName}");
}
Console.WriteLine();

// ── 6. Start batch translation (mirrors TranslateBatch + TranslationService) ──
var targetContainerClient = blobServiceClient.GetBlobContainerClient(targetContainerName);
await targetContainerClient.CreateIfNotExistsAsync();

var sourceContainerUri = new Uri($"https://{storageAccountName}.blob.core.windows.net/{sourceContainerName}");
var targetContainerUri = new Uri($"https://{storageAccountName}.blob.core.windows.net/{targetContainerName}");

var operations = new List<(BatchInfo Batch, DocumentTranslationOperation Operation)>();

foreach (var batch in batches)
{
    var source = new TranslationSource(sourceContainerUri);
    source.Prefix = batch.SourceBlobPrefix;

    var target = new TranslationTarget(targetContainerUri, batch.TargetLanguage);
    var input = new DocumentTranslationInput(source, new[] { target });

    var operation = await translationClient.StartTranslationAsync(input);
    batch.TranslationOperationId = operation.Id;

    operations.Add((batch, operation));
    Console.WriteLine($"Batch {batch.BatchId[..8]} → Operation {operation.Id} started");
}
Console.WriteLine($"✓ {operations.Count} translation operation(s) started");
Console.WriteLine();

// ── 7. Poll for completion (mirrors TranslationService.WaitForTranslationAsync) ──
var results = new List<BatchResult>();

foreach (var (batch, operation) in operations)
{
    Console.WriteLine($"Waiting for batch {batch.BatchId[..8]} (operation {operation.Id})...");

    try
    {
        await operation.WaitForCompletionAsync();

        string? error = null;
        int failedCount = operation.DocumentsFailed;

        if (failedCount > 0)
        {
            var docErrors = new List<string>();
            await foreach (var docStatus in operation.GetDocumentStatusesAsync())
            {
                if (docStatus.Error != null)
                    docErrors.Add($"{docStatus.SourceDocumentUri}: {docStatus.Error.Message}");
            }
            error = string.Join("; ", docErrors);
        }

        var batchStatus = operation.Status switch
        {
            var s when s == DocumentTranslationStatus.Succeeded => "Succeeded",
            var s when s == DocumentTranslationStatus.Failed => "Failed",
            var s when s == DocumentTranslationStatus.Canceled || s == DocumentTranslationStatus.Canceling => "Cancelled",
            _ => "Failed"
        };

        results.Add(new BatchResult(batch.BatchId, batchStatus, operation.DocumentsSucceeded, failedCount, error));

        Console.WriteLine($"  Status: {operation.Status}  |  Succeeded: {operation.DocumentsSucceeded}  |  Failed: {failedCount}");

        await foreach (var docStatus in operation.GetDocumentStatusesAsync())
        {
            Console.WriteLine($"    {docStatus.SourceDocumentUri}");
            Console.WriteLine($"      → Status: {docStatus.Status}");
            if (docStatus.TranslatedDocumentUri != null)
                Console.WriteLine($"      → Output: {docStatus.TranslatedDocumentUri}");
            if (docStatus.Error != null)
                Console.WriteLine($"      → Error:  {docStatus.Error.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ Failed: {ex.Message}");
        results.Add(new BatchResult(batch.BatchId, "Failed", 0, batch.FileCount, $"Translation failed: {ex.Message}"));
    }
    Console.WriteLine();
}

// ── 8. Finalize session (mirrors TranslationOrchestrator.FinalizeSession) ────
var allSucceeded = results.All(r => r.Status == "Succeeded");
var totalTranslated = results.Sum(r => r.TranslatedFileCount);
var totalFailed = results.Sum(r => r.FailedFileCount);
var failedBatchCount = results.Count(r => r.Status != "Succeeded");

Console.WriteLine("=== Session Summary ===");
Console.WriteLine($"Session ID       : {sessionId}");
Console.WriteLine($"Status           : {(allSucceeded ? "Completed" : "Failed")}");
Console.WriteLine($"Total Translated : {totalTranslated}");
Console.WriteLine($"Total Failed     : {totalFailed}");
Console.WriteLine($"Batches          : {results.Count} ({failedBatchCount} failed)");
if (!allSucceeded)
{
    foreach (var r in results.Where(r => r.Error != null))
        Console.WriteLine($"  Batch {r.BatchId[..8]}: {r.Error}");
}
Console.WriteLine();

// ── 9. Download translated documents ─────────────────────────────────────────
var outputDir = Path.Combine(projectDir, "translated_output", sessionId);
Directory.CreateDirectory(outputDir);

int downloadCount = 0;
await foreach (var blob in targetContainerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, $"{sessionId}/", CancellationToken.None))
{
    var blobClient = targetContainerClient.GetBlobClient(blob.Name);
    var localPath = Path.Combine(outputDir, Path.GetFileName(blob.Name));

    using var downloadStream = await blobClient.OpenReadAsync();
    using var fileStream = File.Create(localPath);
    await downloadStream.CopyToAsync(fileStream);

    downloadCount++;
    Console.WriteLine($"  Downloaded: {blob.Name} → {localPath}");
}

Console.WriteLine();
if (downloadCount > 0)
    Console.WriteLine($"✓ {downloadCount} translated file(s) saved to: {outputDir}");
else
    Console.WriteLine("⚠ No translated files found in target container.");

// ═══════════════════════════════════════════════════════════════════════════════
// Helper types & methods
// ═══════════════════════════════════════════════════════════════════════════════

static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
{
    ".pdf" => "application/pdf",
    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    ".html" or ".htm" => "text/html",
    ".txt" => "text/plain",
    ".xlf" or ".xliff" => "application/xliff+xml",
    ".tsv" => "text/tab-separated-values",
    _ => "application/octet-stream"
};

static List<BatchInfo> SplitIntoBatches(List<SourceDocumentInfo> documents, string sessionId, string targetLang)
{
    const int maxFiles = 1000;
    const long maxBytes = 250L * 1024 * 1024;

    var batches = new List<BatchInfo>();
    var current = new BatchInfo(sessionId, targetLang);

    foreach (var doc in documents)
    {
        if (current.FileCount + 1 > maxFiles || current.TotalSize + doc.FileSize > maxBytes)
        {
            if (current.FileCount > 0)
                batches.Add(current);
            current = new BatchInfo(sessionId, targetLang);
        }

        current.Documents.Add(doc);
        current.FileCount++;
        current.TotalSize += doc.FileSize;
    }

    if (current.FileCount > 0)
        batches.Add(current);

    return batches;
}

record SourceDocumentInfo(string FileName, long FileSize, string ContentType);
record BatchResult(string BatchId, string Status, int TranslatedFileCount, int FailedFileCount, string? Error);

class BatchInfo
{
    public string BatchId { get; } = Guid.NewGuid().ToString();
    public string SessionId { get; }
    public string SourceBlobPrefix { get; }
    public string TargetBlobPrefix { get; }
    public string TargetLanguage { get; }
    public string? TranslationOperationId { get; set; }
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public List<SourceDocumentInfo> Documents { get; } = new();

    public BatchInfo(string sessionId, string targetLang)
    {
        SessionId = sessionId;
        SourceBlobPrefix = $"{sessionId}/";
        TargetBlobPrefix = $"{sessionId}/";
        TargetLanguage = targetLang;
    }
}
