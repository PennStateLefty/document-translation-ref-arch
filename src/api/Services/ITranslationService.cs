namespace DocumentTranslation.Api.Services;

using System.Text.Json.Serialization;
using DocumentTranslation.Api.Models;

public interface ITranslationService
{
    Task<string> StartBatchTranslationAsync(Uri sourceContainerUri, Uri targetContainerUri, string targetLanguage, string? sourcePrefix = null);
    Task<TranslationResult> GetTranslationStatusAsync(string operationId, string batchId);
    Task<List<LanguageInfo>> GetSupportedLanguagesAsync();
}

public class LanguageInfo
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
