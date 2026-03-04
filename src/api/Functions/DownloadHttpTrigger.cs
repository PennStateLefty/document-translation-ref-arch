using System.IO.Compression;
using System.Net;
using DocumentTranslation.Api.Models;
using DocumentTranslation.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DocumentTranslation.Api.Functions;

public class DownloadHttpTrigger
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<DownloadHttpTrigger> _logger;

    public DownloadHttpTrigger(IBlobStorageService blobStorageService, ILogger<DownloadHttpTrigger> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    [Function("DownloadTranslation")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "translate/{sessionId}/download")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        string sessionId)
    {
        _logger.LogInformation("Download request for session {SessionId}", sessionId);

        var instance = await durableClient.GetInstanceAsync(sessionId);
        if (instance == null)
        {
            _logger.LogWarning("Download requested for unknown session {SessionId}", sessionId);
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Translation session not found." });
            return notFound;
        }

        // Check if the orchestration has completed
        if (instance.RuntimeStatus != OrchestrationRuntimeStatus.Completed 
            && instance.RuntimeStatus != OrchestrationRuntimeStatus.Failed)
        {
            _logger.LogInformation("Download requested for in-progress session {SessionId} (status: {RuntimeStatus})",
                sessionId, instance.RuntimeStatus);
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteAsJsonAsync(new { error = "Translation is still in progress. Please wait for completion." });
            return conflict;
        }

        // List translated files
        var translatedFiles = await _blobStorageService.ListBlobsAsync("translated-documents", $"{sessionId}/");

        if (translatedFiles.Count == 0)
        {
            _logger.LogWarning("No translated files found for session {SessionId}", sessionId);
            var noFiles = req.CreateResponse(HttpStatusCode.NotFound);
            await noFiles.WriteAsJsonAsync(new { error = "No translated files available for this session." });
            return noFiles;
        }

        // Create zip archive
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var blobPath in translatedFiles)
            {
                var fileName = Path.GetFileName(blobPath);
                var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                using var blobStream = await _blobStorageService.DownloadBlobAsync("translated-documents", blobPath);
                await blobStream.CopyToAsync(entryStream);
            }
        }

        zipStream.Position = 0;

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/zip");
        response.Headers.Add("Content-Disposition", $"attachment; filename=\"translated-{sessionId}.zip\"");
        response.Body = zipStream;

        _logger.LogInformation("Download prepared for session {SessionId} with {FileCount} files", 
            sessionId, translatedFiles.Count);

        return response;
    }
}
