using Azure.Identity;
using DocumentTranslation.Api.Models;
using Microsoft.Extensions.Logging;

namespace DocumentTranslation.Api.Services;

public class TranslationService : ITranslationService
{
    private readonly ILogger<TranslationService> _logger;
    private readonly string? _translatorEndpoint;

    public TranslationService(ILogger<TranslationService> logger)
    {
        _logger = logger;
        _translatorEndpoint = Environment.GetEnvironmentVariable("TRANSLATOR_ENDPOINT");
    }

    public async Task<string> StartBatchTranslationAsync(
        Uri sourceContainerUri, Uri targetContainerUri, string targetLanguage)
    {
        _logger.LogInformation("Starting batch translation to {TargetLanguage}", targetLanguage);
        _logger.LogInformation("Source: {SourceUri}", sourceContainerUri);
        _logger.LogInformation("Target: {TargetUri}", targetContainerUri);

        if (string.IsNullOrEmpty(_translatorEndpoint))
        {
            _logger.LogWarning("TRANSLATOR_ENDPOINT not configured. Returning mock operation ID.");
            return $"mock-operation-{Guid.NewGuid()}";
        }

        try
        {
            // Use DefaultAzureCredential — Translator MI authenticates to storage via RBAC
            var credential = new DefaultAzureCredential();
            var client = new Azure.AI.Translation.Document.DocumentTranslationClient(
                new Uri(_translatorEndpoint), credential);

            // Plain container URIs work — Translator MI has Storage Blob Data Contributor role
            var input = new Azure.AI.Translation.Document.DocumentTranslationInput(
                sourceContainerUri, targetContainerUri, targetLanguage);

            var operation = await client.StartTranslationAsync(input);

            _logger.LogInformation("Translation operation started: {OperationId}", operation.Id);
            return operation.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start batch translation");
            throw;
        }
    }

    public async Task<TranslationResult> GetTranslationStatusAsync(string operationId, string batchId)
    {
        _logger.LogInformation("Checking status for operation {OperationId}", operationId);

        if (operationId.StartsWith("mock-operation-"))
        {
            _logger.LogInformation("Mock operation — returning succeeded status");
            return new TranslationResult
            {
                BatchId = batchId,
                Status = BatchStatus.Succeeded,
                TranslatedFileCount = 1,
                FailedFileCount = 0
            };
        }

        try
        {
            var credential = new DefaultAzureCredential();
            var client = new Azure.AI.Translation.Document.DocumentTranslationClient(
                new Uri(_translatorEndpoint!), credential);

            var operation = new Azure.AI.Translation.Document.DocumentTranslationOperation(operationId, client);
            await operation.UpdateStatusAsync();

            BatchStatus status;
            if (operation.Status == Azure.AI.Translation.Document.DocumentTranslationStatus.Succeeded)
                status = BatchStatus.Succeeded;
            else if (operation.Status == Azure.AI.Translation.Document.DocumentTranslationStatus.Failed)
                status = BatchStatus.Failed;
            else if (operation.Status == Azure.AI.Translation.Document.DocumentTranslationStatus.Canceled)
                status = BatchStatus.Cancelled;
            else if (operation.Status == Azure.AI.Translation.Document.DocumentTranslationStatus.Running)
                status = BatchStatus.Running;
            else
                status = BatchStatus.Submitted;

            return new TranslationResult
            {
                BatchId = batchId,
                Status = status,
                TranslatedFileCount = operation.DocumentsSucceeded,
                FailedFileCount = operation.DocumentsFailed,
                Error = status == BatchStatus.Failed ? "Translation operation failed." : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get translation status for operation {OperationId}", operationId);
            return new TranslationResult
            {
                BatchId = batchId,
                Status = BatchStatus.Failed,
                Error = $"Failed to check status: {ex.Message}"
            };
        }
    }

    public async Task<List<LanguageInfo>> GetSupportedLanguagesAsync()
    {
        _logger.LogInformation("Fetching supported languages");

        try
        {
            // Call the Azure Translator languages API
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(
                "https://api.cognitive.microsofttranslator.com/languages?api-version=3.0&scope=translation");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = System.Text.Json.JsonDocument.Parse(content);
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
