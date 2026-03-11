using System.Net;
using DocumentTranslation.Api.Models;
using DocumentTranslation.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DocumentTranslation.Api.Functions;

public class TranslateHttpTrigger
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<TranslateHttpTrigger> _logger;

    public TranslateHttpTrigger(IBlobStorageService blobStorageService, ILogger<TranslateHttpTrigger> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    [Function("StartTranslation")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "translate")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient)
    {
        _logger.LogInformation("Received translation request");

        // Parse multipart form data from the request body
        var boundary = GetBoundary(req.Headers);
        if (boundary == null)
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                "No files provided. Please select at least one file to translate.");
        }

        var (formFields, multipartFiles) = await ParseMultipartBodyAsync(req.Body, boundary);
        var targetLanguage = formFields.GetValueOrDefault("targetLanguage");

        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                "Target language is required. Please select a language.");
        }

        var files = new List<SourceDocument>();
        var errors = new List<string>();

        {
            
            foreach (var (fileName, fileData) in multipartFiles)
            {
                if (!SourceDocument.IsSupportedExtension(fileName))
                {
                    errors.Add($"{fileName}: Unsupported file type. Supported types: PDF, DOCX, XLSX, PPTX, HTML, TXT.");
                    continue;
                }

                if (fileData.Length > SourceDocument.MaxFileSize)
                {
                    errors.Add($"{fileName}: File too large. Maximum file size is 30 MB.");
                    continue;
                }

                files.Add(new SourceDocument
                {
                    FileName = fileName,
                    FileSize = fileData.Length,
                    ContentType = GetContentType(fileName)
                });
            }

            if (errors.Count > 0)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                    "Some files could not be accepted.", errors);
            }

            if (files.Count == 0)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                    "No files provided. Please select at least one file to translate.");
            }

            // Create session
            var session = new TranslationSession
            {
                SessionId = Guid.NewGuid().ToString(),
                TargetLanguage = targetLanguage,
                TotalFileCount = files.Count,
                TotalFileSize = files.Sum(f => f.FileSize),
                Status = TranslationStatus.Uploading,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Upload files to blob storage
            foreach (var (fileName, fileData) in multipartFiles)
            {
                if (SourceDocument.IsSupportedExtension(fileName) && fileData.Length <= SourceDocument.MaxFileSize)
                {
                    using var stream = new MemoryStream(fileData);
                    await _blobStorageService.UploadFileAsync(
                        "source-documents",
                        $"{session.SessionId}/{fileName}",
                        stream,
                        GetContentType(fileName));
                }
            }

            _logger.LogInformation("Session {SessionId} created with {FileCount} files ({TotalSize} bytes)",
                session.SessionId, session.TotalFileCount, session.TotalFileSize);

            // Start Durable Functions orchestration
            var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                "TranslationSessionOrchestrator", session, new StartOrchestrationOptions(session.SessionId));

            _logger.LogInformation("Orchestration {InstanceId} started for session {SessionId}", instanceId, session.SessionId);

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                sessionId = session.SessionId,
                status = "Uploading",
                statusUrl = $"/api/translate/{session.SessionId}",
                createdAt = session.CreatedAt
            });
            return response;
        }
    }

    private static string? GetBoundary(HttpHeadersCollection headers)
    {
        if (headers.TryGetValues("Content-Type", out var contentTypeValues))
        {
            var contentType = contentTypeValues.FirstOrDefault();
            if (contentType != null && contentType.Contains("multipart/form-data"))
            {
                var boundaryIndex = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
                if (boundaryIndex >= 0)
                {
                    return contentType[(boundaryIndex + 9)..].Trim('"', ' ');
                }
            }
        }
        return null;
    }

    private static async Task<(Dictionary<string, string> Fields, List<(string FileName, byte[] Data)> Files)>
        ParseMultipartBodyAsync(Stream body, string boundary)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<(string, byte[])>();

        // Read the entire body as raw bytes to preserve binary file content.
        using var ms = new MemoryStream();
        await body.CopyToAsync(ms);
        var allBytes = ms.ToArray();

        var boundaryBytes = System.Text.Encoding.UTF8.GetBytes($"--{boundary}");
        var headerSeparator = new byte[] { 0x0D, 0x0A, 0x0D, 0x0A }; // \r\n\r\n

        var partPositions = FindAllOccurrences(allBytes, boundaryBytes);

        for (int i = 0; i < partPositions.Count - 1; i++)
        {
            var partStart = partPositions[i] + boundaryBytes.Length;
            var partEnd = partPositions[i + 1];

            // Skip leading \r\n after boundary
            if (partStart + 1 < allBytes.Length && allBytes[partStart] == 0x0D && allBytes[partStart + 1] == 0x0A)
                partStart += 2;

            // Find the header/body separator within this part
            var headerEndIdx = FindSequence(allBytes, headerSeparator, partStart, partEnd);
            if (headerEndIdx < 0) continue;

            var headerText = System.Text.Encoding.UTF8.GetString(allBytes, partStart, headerEndIdx - partStart);
            var bodyStart = headerEndIdx + headerSeparator.Length;

            // Trim trailing \r\n before next boundary
            var bodyEnd = partEnd;
            if (bodyEnd >= 2 && allBytes[bodyEnd - 1] == 0x0A && allBytes[bodyEnd - 2] == 0x0D)
                bodyEnd -= 2;

            if (headerText.Contains("filename="))
            {
                var fileNameMatch = System.Text.RegularExpressions.Regex.Match(headerText, @"filename=""?([^"";\r\n]+)""?");
                if (!fileNameMatch.Success) continue;

                var fileName = fileNameMatch.Groups[1].Value.Trim();
                var fileBytes = new byte[bodyEnd - bodyStart];
                Array.Copy(allBytes, bodyStart, fileBytes, 0, fileBytes.Length);
                files.Add((fileName, fileBytes));
            }
            else
            {
                var nameMatch = System.Text.RegularExpressions.Regex.Match(headerText, @"name=""?([^"";\r\n]+)""?");
                if (nameMatch.Success)
                {
                    var value = System.Text.Encoding.UTF8.GetString(allBytes, bodyStart, bodyEnd - bodyStart).Trim();
                    fields[nameMatch.Groups[1].Value.Trim()] = value;
                }
            }
        }

        return (fields, files);
    }

    private static List<int> FindAllOccurrences(byte[] data, byte[] pattern)
    {
        var positions = new List<int>();
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) positions.Add(i);
        }
        return positions;
    }

    private static int FindSequence(byte[] data, byte[] pattern, int start, int end)
    {
        var limit = Math.Min(end, data.Length) - pattern.Length;
        for (int i = start; i <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    private static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
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

    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req, HttpStatusCode statusCode, string error, List<string>? details = null)
    {
        var response = req.CreateResponse(statusCode);
        var body = new Dictionary<string, object> { ["error"] = error };
        if (details != null && details.Count > 0)
            body["details"] = details;
        await response.WriteAsJsonAsync(body);
        response.StatusCode = statusCode;
        return response;
    }
}
