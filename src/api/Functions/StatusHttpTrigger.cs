using System.Net;
using DocumentTranslation.Api.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
namespace DocumentTranslation.Api.Functions;

public class StatusHttpTrigger
{
    private readonly ILogger<StatusHttpTrigger> _logger;

    public StatusHttpTrigger(ILogger<StatusHttpTrigger> logger)
    {
        _logger = logger;
    }

    [Function("GetTranslationStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "translate/{sessionId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        string sessionId)
    {
        _logger.LogInformation("Status request for session {SessionId}", sessionId);

        var instance = await durableClient.GetInstanceAsync(sessionId);

        if (instance == null)
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Translation session not found." });
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);

        // Parse the orchestration output or custom status
        var session = instance.ReadOutputAs<TranslationSession>();
        
        if (session != null && (instance.RuntimeStatus == OrchestrationRuntimeStatus.Completed 
            || instance.RuntimeStatus == OrchestrationRuntimeStatus.Failed))
        {
            var statusResponse = new
            {
                sessionId = session.SessionId,
                status = session.Status.ToString(),
                targetLanguage = session.TargetLanguage,
                totalFiles = session.TotalFileCount,
                createdAt = session.CreatedAt,
                batches = session.Batches.Select(b => new
                {
                    batchId = b.BatchId,
                    status = b.Status.ToString(),
                    fileCount = b.FileCount,
                    translatedFileCount = b.Status == BatchStatus.Succeeded ? b.FileCount : 0,
                    error = b.Error
                }),
                downloadUrl = session.Status == TranslationStatus.Completed || session.Status == TranslationStatus.Failed
                    ? $"/api/translate/{sessionId}/download" : (string?)null,
                error = session.Error
            };
            await response.WriteAsJsonAsync(statusResponse);
        }
        else
        {
            // Orchestration is still running
            var input = instance.ReadInputAs<TranslationSession>();
            var statusStr = instance.RuntimeStatus switch
            {
                OrchestrationRuntimeStatus.Pending => "Uploading",
                OrchestrationRuntimeStatus.Running => "Processing",
                OrchestrationRuntimeStatus.Failed => "Error",
                _ => "Processing"
            };

            await response.WriteAsJsonAsync(new
            {
                sessionId,
                status = statusStr,
                targetLanguage = input?.TargetLanguage ?? "",
                totalFiles = input?.TotalFileCount ?? 0,
                createdAt = input?.CreatedAt ?? DateTimeOffset.UtcNow,
                batches = Array.Empty<object>()
            });
        }

        return response;
    }
}
