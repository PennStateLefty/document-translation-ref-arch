using DocumentTranslation.Api.Models;
using DocumentTranslation.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DocumentTranslation.Api.Functions;

public class TranslationOrchestrator
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ITranslationService _translationService;
    private readonly ILogger<TranslationOrchestrator> _logger;

    public TranslationOrchestrator(
        IBlobStorageService blobStorageService,
        ITranslationService translationService,
        ILogger<TranslationOrchestrator> logger)
    {
        _blobStorageService = blobStorageService;
        _translationService = translationService;
        _logger = logger;
    }

    [Function("TranslationSessionOrchestrator")]
    public async Task<TranslationSession> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var session = context.GetInput<TranslationSession>()!;
        var logger = context.CreateReplaySafeLogger<TranslationOrchestrator>();

        try
        {
            logger.LogInformation("Orchestration started for session {SessionId}", session.SessionId);

            // Step 1: Assess and split into batches
            session.Status = TranslationStatus.Processing;
            var batches = await context.CallActivityAsync<List<TranslationBatch>>(
                "AssessAndSplitBatches", session);
            session.Batches = batches;

            logger.LogInformation("Session {SessionId} split into {BatchCount} batches",
                session.SessionId, batches.Count);

            // Step 2: Fan-out — translate all batches in parallel
            var retryOptions = new TaskRetryOptions(new RetryPolicy(
                maxNumberOfAttempts: 3,
                firstRetryInterval: TimeSpan.FromSeconds(5),
                backoffCoefficient: 2.0,
                maxRetryInterval: TimeSpan.FromMinutes(5)));

            var translationTasks = new List<Task<TranslationResult>>();
            foreach (var batch in batches)
            {
                var task = context.CallActivityAsync<TranslationResult>(
                    "TranslateBatch",
                    batch,
                    new TaskOptions(retryOptions));
                translationTasks.Add(task);
            }

            // Step 3: Fan-in — await all results
            var results = await Task.WhenAll(translationTasks);

            // Step 4: Finalize session
            session = await context.CallActivityAsync<TranslationSession>(
                "FinalizeSession",
                new FinalizeSessionInput { Session = session, Results = results.ToList() });

            logger.LogInformation("Session {SessionId} completed with status {Status}",
                session.SessionId, session.Status);

            return session;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Orchestration failed for session {SessionId}", session.SessionId);
            session.Status = TranslationStatus.Error;
            session.Error = $"An unexpected error occurred: {ex.Message}";
            return session;
        }
    }

    /// <summary>
    /// T039: Assess and split batches with dual-constraint splitting.
    /// Respects both file count limit (1,000) and size limit (250 MB).
    /// Both limits evaluated simultaneously; the more restrictive determines split points.
    /// </summary>
    [Function("AssessAndSplitBatches")]
    public async Task<List<TranslationBatch>> AssessAndSplitBatches(
        [ActivityTrigger] TranslationSession session)
    {
        _logger.LogInformation("Assessing batches for session {SessionId}: {FileCount} files, {TotalSize} bytes",
            session.SessionId, session.TotalFileCount, session.TotalFileSize);

        // Get actual file list from blob storage
        var blobNames = await _blobStorageService.ListBlobsAsync("source-documents", $"{session.SessionId}/");

        if (blobNames.Count == 0)
        {
            _logger.LogWarning("No files found in blob storage for session {SessionId}", session.SessionId);
            return new List<TranslationBatch>
            {
                CreateSingleBatch(session)
            };
        }

        // Build document list with sizes (approximate from session total if needed)
        var documents = blobNames.Select(name => new SourceDocument
        {
            FileName = Path.GetFileName(name),
            BlobUrl = name,
            FileSize = session.TotalFileSize / Math.Max(session.TotalFileCount, 1) // Approximate per-file size
        }).ToList();

        return SplitIntoBatches(documents, session.SessionId, session.TargetLanguage);
    }

    /// <summary>
    /// Core batch splitting logic — public static for testability.
    /// Splits documents into batches respecting both constraints simultaneously:
    /// - Max 1,000 files per batch
    /// - Max 250 MB per batch
    /// </summary>
    public static List<TranslationBatch> SplitIntoBatches(List<SourceDocument> documents, string sessionId, string targetLanguage = "")
    {
        var batches = new List<TranslationBatch>();
        var currentBatch = new TranslationBatch
        {
            BatchId = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            SourceBlobPrefix = $"{sessionId}/",
            TargetBlobPrefix = $"{sessionId}/",
            TargetLanguage = targetLanguage
        };

        foreach (var doc in documents)
        {
            // Check if adding this file would exceed either limit
            bool wouldExceedFileCount = currentBatch.FileCount + 1 > TranslationBatch.MaxFilesPerBatch;
            bool wouldExceedSize = currentBatch.TotalSize + doc.FileSize > TranslationBatch.MaxBytesPerBatch;

            if (wouldExceedFileCount || wouldExceedSize)
            {
                // Current batch is full — save it and start a new one
                if (currentBatch.FileCount > 0)
                {
                    batches.Add(currentBatch);
                }

                currentBatch = new TranslationBatch
                {
                    BatchId = Guid.NewGuid().ToString(),
                    SessionId = sessionId,
                    SourceBlobPrefix = $"{sessionId}/",
                    TargetBlobPrefix = $"{sessionId}/",
                    TargetLanguage = targetLanguage
                };
            }

            currentBatch.Documents.Add(doc);
            currentBatch.FileCount++;
            currentBatch.TotalSize += doc.FileSize;
        }

        // Add the last batch
        if (currentBatch.FileCount > 0)
        {
            batches.Add(currentBatch);
        }

        return batches;
    }

    /// <summary>
    /// T041/T042: Translate a single batch using the Document Translation batch API.
    /// Starts translation, then polls for completion.
    /// </summary>
    [Function("TranslateBatch")]
    public async Task<TranslationResult> TranslateBatch(
        [ActivityTrigger] TranslationBatch batch)
    {
        _logger.LogInformation("Starting translation for batch {BatchId} ({FileCount} files, {TotalSize} bytes)",
            batch.BatchId, batch.FileCount, batch.TotalSize);

        batch.Status = BatchStatus.Submitted;

        try
        {
            // Build container-level URIs — Document Translation API expects container URIs
            // with prefix passed via DocumentTranslationInput filter, not baked into the path
            var storageAccountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME")
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage__accountName")
                ?? throw new InvalidOperationException("STORAGE_ACCOUNT_NAME is not configured.");
            var sourceContainerUri = new Uri(
                $"https://{storageAccountName}.blob.core.windows.net/source-documents");
            var targetContainerUri = new Uri(
                $"https://{storageAccountName}.blob.core.windows.net/translated-documents");

            // Start the batch translation using the target language from the session
            var operationId = await _translationService.StartBatchTranslationAsync(
                sourceContainerUri, targetContainerUri,
                batch.TargetLanguage,
                batch.SourceBlobPrefix);

            batch.TranslationOperationId = operationId;
            batch.Status = BatchStatus.Running;

            _logger.LogInformation("Batch {BatchId} submitted as operation {OperationId}",
                batch.BatchId, operationId);

            // Poll for completion
            var result = await MonitorBatchTranslation(batch);

            _logger.LogInformation("Batch {BatchId} completed with status {Status}",
                batch.BatchId, result.Status);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch {BatchId} translation failed", batch.BatchId);
            return new TranslationResult
            {
                BatchId = batch.BatchId,
                Status = BatchStatus.Failed,
                TranslatedFileCount = 0,
                FailedFileCount = batch.FileCount,
                Error = $"Translation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// T042: Monitor a batch translation operation until it reaches a terminal state.
    /// Polls the Document Translation API at increasing intervals.
    /// </summary>
    private async Task<TranslationResult> MonitorBatchTranslation(TranslationBatch batch)
    {
        if (string.IsNullOrEmpty(batch.TranslationOperationId))
        {
            return new TranslationResult
            {
                BatchId = batch.BatchId,
                Status = BatchStatus.Failed,
                Error = "No operation ID available for monitoring."
            };
        }

        var maxAttempts = 360; // 30 minutes at 5-second intervals
        var pollInterval = TimeSpan.FromSeconds(5);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var result = await _translationService.GetTranslationStatusAsync(
                batch.TranslationOperationId, batch.BatchId);

            if (result.Status == BatchStatus.Succeeded ||
                result.Status == BatchStatus.Failed ||
                result.Status == BatchStatus.PartiallySucceeded ||
                result.Status == BatchStatus.Cancelled)
            {
                return result;
            }

            await Task.Delay(pollInterval);
        }

        return new TranslationResult
        {
            BatchId = batch.BatchId,
            Status = BatchStatus.Failed,
            Error = "Translation timed out after 30 minutes."
        };
    }

    /// <summary>
    /// T043: Finalize session by aggregating batch results into session-level status.
    /// All succeeded → Completed. Any failed → Failed with details.
    /// </summary>
    [Function("FinalizeSession")]
    public Task<TranslationSession> FinalizeSession(
        [ActivityTrigger] FinalizeSessionInput input)
    {
        var session = input.Session;
        var results = input.Results;

        _logger.LogInformation("Finalizing session {SessionId} with {ResultCount} batch results",
            session.SessionId, results.Count);

        // Update batch statuses from results
        foreach (var result in results)
        {
            var batch = session.Batches.FirstOrDefault(b => b.BatchId == result.BatchId);
            if (batch != null)
            {
                batch.Status = result.Status;
                batch.Error = result.Error;
            }
        }

        // Determine overall session status
        var allSucceeded = results.All(r => r.Status == BatchStatus.Succeeded);
        var allFailed = results.All(r => r.Status == BatchStatus.Failed);
        var totalTranslated = results.Sum(r => r.TranslatedFileCount);
        var totalFailed = results.Sum(r => r.FailedFileCount);
        var failedBatchCount = results.Count(r => r.Status != BatchStatus.Succeeded);

        if (allSucceeded)
        {
            session.Status = TranslationStatus.Completed;
            _logger.LogInformation("Session {SessionId} completed successfully. {TranslatedCount} files translated.",
                session.SessionId, totalTranslated);
        }
        else
        {
            session.Status = TranslationStatus.Failed;
            session.Error = $"Translation partially failed. {failedBatchCount} of {results.Count} batches failed.";
            _logger.LogWarning("Session {SessionId} partially failed. {FailedBatches}/{TotalBatches} batches failed. {TranslatedCount} files translated, {FailedCount} files failed.",
                session.SessionId, failedBatchCount, results.Count, totalTranslated, totalFailed);
        }

        return Task.FromResult(session);
    }

    private static TranslationBatch CreateSingleBatch(TranslationSession session)
    {
        return new TranslationBatch
        {
            BatchId = Guid.NewGuid().ToString(),
            SessionId = session.SessionId,
            FileCount = session.TotalFileCount,
            TotalSize = session.TotalFileSize,
            SourceBlobPrefix = $"{session.SessionId}/",
            TargetBlobPrefix = $"{session.SessionId}/",
            TargetLanguage = session.TargetLanguage,
            Status = BatchStatus.Pending
        };
    }
}

public class FinalizeSessionInput
{
    public TranslationSession Session { get; set; } = new();
    public List<TranslationResult> Results { get; set; } = new();
}
