using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using DocumentTranslation.Api.Models;
using Microsoft.Extensions.Logging;

namespace DocumentTranslation.Api.Services;

/// <summary>
/// Calls the Document Translation REST API directly with bearer-token auth,
/// modeled after the working Python function app (no SDK dependency).
/// </summary>
public class TranslationService : ITranslationService
{
    private const string ApiVersion = "2024-05-01";
    private static readonly string[] TokenScopes = ["https://cognitiveservices.azure.com/.default"];

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly ILogger<TranslationService> _logger;
    private readonly string? _aiServicesEndpoint;

    public TranslationService(HttpClient httpClient, TokenCredential credential, ILogger<TranslationService> logger)
    {
        _httpClient = httpClient;
        _credential = credential;
        _logger = logger;
        _aiServicesEndpoint = Environment.GetEnvironmentVariable("AI_SERVICES_ENDPOINT")?.TrimEnd('/');
    }

    private async Task<string> GetBearerTokenAsync()
    {
        var tokenResult = await _credential.GetTokenAsync(
            new TokenRequestContext(TokenScopes), CancellationToken.None);
        return tokenResult.Token;
    }

    public async Task<string> StartBatchTranslationAsync(
        Uri sourceContainerUri, Uri targetContainerUri, string targetLanguage, string? sourcePrefix = null)
    {
        _logger.LogInformation("Starting batch translation to {TargetLanguage}", targetLanguage);
        _logger.LogInformation("Source: {SourceUri}, Prefix: {Prefix}", sourceContainerUri, sourcePrefix);
        _logger.LogInformation("Target: {TargetUri}", targetContainerUri);

        if (string.IsNullOrEmpty(_aiServicesEndpoint))
        {
            _logger.LogWarning("AI_SERVICES_ENDPOINT not configured. Returning mock operation ID.");
            return $"mock-operation-{Guid.NewGuid()}";
        }

        // Build the batch request per the Document Translation REST API
        // POST {endpoint}/translator/document/batches?api-version=2024-05-01
        var sourceObj = new Dictionary<string, object>
        {
            ["sourceUrl"] = sourceContainerUri.ToString()
        };
        if (!string.IsNullOrEmpty(sourcePrefix))
        {
            sourceObj["filter"] = new { prefix = sourcePrefix };
        }

        var body = new
        {
            inputs = new[]
            {
                new
                {
                    source = sourceObj,
                    targets = new[]
                    {
                        new
                        {
                            targetUrl = targetContainerUri.ToString(),
                            language = targetLanguage
                        }
                    }
                }
            }
        };

        var url = $"{_aiServicesEndpoint}/translator/document/batches?api-version={ApiVersion}";
        var token = await GetBearerTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Batch translation start failed ({StatusCode}): {Body}",
                (int)response.StatusCode, responseBody);
            throw new HttpRequestException(
                $"Document Translation API returned {(int)response.StatusCode}: {responseBody}");
        }

        // Operation-Location header contains the status URL including the operation ID
        // e.g. https://.../translator/document/batches/{id}?api-version=...
        if (response.Headers.TryGetValues("Operation-Location", out var locationValues))
        {
            var operationUrl = locationValues.First();
            _logger.LogInformation("Translation operation started: {OperationUrl}", operationUrl);
            // Return the full operation URL — we'll poll it directly
            return operationUrl;
        }

        throw new InvalidOperationException(
            "Document Translation API did not return an Operation-Location header.");
    }

    public async Task<TranslationResult> GetTranslationStatusAsync(string operationUrl, string batchId)
    {
        _logger.LogInformation("Checking status at {OperationUrl}", operationUrl);

        if (operationUrl.StartsWith("mock-operation-"))
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
            var token = await GetBearerTokenAsync();

            using var request = new HttpRequestMessage(HttpMethod.Get, operationUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Status check failed ({StatusCode}): {Body}",
                    (int)response.StatusCode, body);
                return new TranslationResult
                {
                    BatchId = batchId,
                    Status = BatchStatus.Failed,
                    Error = $"Status API returned {(int)response.StatusCode}: {body}"
                };
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var statusStr = root.GetProperty("status").GetString() ?? "";

            var status = statusStr.ToLowerInvariant() switch
            {
                "succeeded" => BatchStatus.Succeeded,
                "failed" => BatchStatus.Failed,
                "cancelled" or "cancelling" => BatchStatus.Cancelled,
                "running" => BatchStatus.Running,
                "notstarted" => BatchStatus.Submitted,
                "validating" => BatchStatus.Submitted,
                _ => BatchStatus.Submitted
            };

            int translatedCount = 0, failedCount = 0;
            if (root.TryGetProperty("summary", out var summary))
            {
                translatedCount = summary.TryGetProperty("success", out var s) ? s.GetInt32() : 0;
                failedCount = summary.TryGetProperty("failed", out var f) ? f.GetInt32() : 0;
            }

            string? error = null;
            if (status == BatchStatus.Failed && root.TryGetProperty("error", out var errorEl))
            {
                error = errorEl.TryGetProperty("message", out var msg) ? msg.GetString() : "Translation failed.";
            }

            return new TranslationResult
            {
                BatchId = batchId,
                Status = status,
                TranslatedFileCount = translatedCount,
                FailedFileCount = failedCount,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get translation status");
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
            // Languages endpoint doesn't require auth
            string url;
            if (!string.IsNullOrEmpty(_aiServicesEndpoint))
            {
                url = $"{_aiServicesEndpoint}/translator/text/v3.0/languages?scope=translation";
            }
            else
            {
                url = "https://api.cognitive.microsofttranslator.com/languages?api-version=3.0&scope=translation";
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
