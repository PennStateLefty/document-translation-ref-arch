namespace DocumentTranslation.Api.Services;

using System.Text.Json.Serialization;
using Azure.AI.Translation.Document;
using DocumentTranslation.Api.Models;

public interface ITranslationService
{
    Task<DocumentTranslationOperation> StartBatchTranslationAsync(Uri sourceContainerUri, Uri targetContainerUri, string targetLanguage, string? sourcePrefix = null);
    Task<TranslationResult> WaitForTranslationAsync(DocumentTranslationOperation operation, string batchId);
    Task<List<LanguageInfo>> GetSupportedLanguagesAsync();
}

public class LanguageInfo
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
