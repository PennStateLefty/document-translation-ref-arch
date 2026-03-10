using System.Text.Json;
using Azure.AI.Translation.Document;
using Azure.Core;
using DocumentTranslation.Api.Models;
using Microsoft.Extensions.Logging;

namespace DocumentTranslation.Api.Services;

/// <summary>
/// Translates documents using the Azure.AI.Translation.Document SDK v2.0.0
/// with managed-identity (TokenCredential) authentication.
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly DocumentTranslationClient _client;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TranslationService> _logger;
    private readonly string? _aiServicesEndpoint;

    public TranslationService(
        DocumentTranslationClient client,
        HttpClient httpClient,
        ILogger<TranslationService> logger)
    {
        _client = client;
        _httpClient = httpClient;
        _logger = logger;
        _aiServicesEndpoint = Environment.GetEnvironmentVariable("AI_SERVICES_ENDPOINT")?.TrimEnd('/');
    }

    public async Task<DocumentTranslationOperation> StartBatchTranslationAsync(
        Uri sourceContainerUri, Uri targetContainerUri, string targetLanguage, string? sourcePrefix = null)
    {
        _logger.LogInformation("Starting batch translation to {TargetLanguage}", targetLanguage);
        _logger.LogInformation("Source: {SourceUri}, Prefix: {Prefix}", sourceContainerUri, sourcePrefix);
        _logger.LogInformation("Target: {TargetUri}", targetContainerUri);

        var source = new TranslationSource(sourceContainerUri);
        if (!string.IsNullOrEmpty(sourcePrefix))
        {
            source.Prefix = sourcePrefix;
        }

        var target = new TranslationTarget(targetContainerUri, targetLanguage);

        var input = new DocumentTranslationInput(source, new[] { target });

        var operation = await _client.StartTranslationAsync(input);

        _logger.LogInformation("Translation operation started: {OperationId}", operation.Id);

        return operation;
    }

    public async Task<TranslationResult> WaitForTranslationAsync(DocumentTranslationOperation operation, string batchId)
    {
        _logger.LogInformation("Waiting for translation operation {OperationId} to complete", operation.Id);

        try
        {
            await operation.WaitForCompletionAsync();

            _logger.LogInformation("Operation {OperationId} completed with status {Status}",
                operation.Id, operation.Status);

            string? error = null;
            int failedCount = operation.DocumentsFailed;

            if (failedCount > 0)
            {
                // Collect error details from failed documents
                var errors = new List<string>();
                await foreach (var docStatus in operation.GetDocumentStatusesAsync())
                {
                    if (docStatus.Error != null)
                    {
                        errors.Add($"{docStatus.SourceDocumentUri}: {docStatus.Error.Message}");
                    }
                }
                error = string.Join("; ", errors);
            }

            var status = operation.Status switch
            {
                var s when s == DocumentTranslationStatus.Succeeded => BatchStatus.Succeeded,
                var s when s == DocumentTranslationStatus.Failed => BatchStatus.Failed,
                var s when s == DocumentTranslationStatus.Canceled || s == DocumentTranslationStatus.Canceling
                    => BatchStatus.Cancelled,
                _ => BatchStatus.Failed
            };

            return new TranslationResult
            {
                BatchId = batchId,
                Status = status,
                TranslatedFileCount = operation.DocumentsSucceeded,
                FailedFileCount = failedCount,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed waiting for translation operation {OperationId}", operation.Id);
            return new TranslationResult
            {
                BatchId = batchId,
                Status = BatchStatus.Failed,
                Error = $"Translation failed: {ex.Message}"
            };
        }
    }

    public async Task<List<LanguageInfo>> GetSupportedLanguagesAsync()
    {
        _logger.LogInformation("Fetching supported languages");

        try
        {
            // Languages endpoint doesn't require auth
            string url;
            if (!string.IsNullOrEmpty(_aiServicesEndpoint))
            {
                url = $"{_aiServicesEndpoint}/languages?api-version=2025-10-01-preview";
            }
            else
            {
                url = "https://api.cognitive.microsofttranslator.com/languages?api-version=2025-10-01-preview";
            }

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var json = JsonDocument.Parse(content);
                var languages = new List<LanguageInfo>();

                if (json.RootElement.TryGetProperty("translation", out var translationElement))
                {
                    foreach (var lang in translationElement.EnumerateObject())
                    {
                        var name = lang.Value.GetProperty("name").GetString() ?? lang.Name;
                        languages.Add(new LanguageInfo { Code = lang.Name, Name = name });
                    }
                }

                return languages.OrderBy(l => l.Name).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch languages from API, using fallback list");
        }

        // Fallback: return common languages
        return new List<LanguageInfo>
        {
            new() { Code = "ar", Name = "Arabic" },
            new() { Code = "zh-Hans", Name = "Chinese (Simplified)" },
            new() { Code = "nl", Name = "Dutch" },
            new() { Code = "en", Name = "English" },
            new() { Code = "fr", Name = "French" },
            new() { Code = "de", Name = "German" },
            new() { Code = "hi", Name = "Hindi" },
            new() { Code = "it", Name = "Italian" },
            new() { Code = "ja", Name = "Japanese" },
            new() { Code = "ko", Name = "Korean" },
            new() { Code = "pt", Name = "Portuguese" },
            new() { Code = "ru", Name = "Russian" },
            new() { Code = "es", Name = "Spanish" },
            new() { Code = "tr", Name = "Turkish" },
            new() { Code = "vi", Name = "Vietnamese" }
        };
    }
}
