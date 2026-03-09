namespace DocumentTranslation.Api.Services;

using DocumentTranslation.Api.Models;

public interface ITranslationService
{
    Task<string> StartBatchTranslationAsync(Uri sourceContainerUri, Uri targetContainerUri, string targetLanguage);
    Task<TranslationResult> GetTranslationStatusAsync(string operationId, string batchId);
    Task<List<LanguageInfo>> GetSupportedLanguagesAsync();
}

public class LanguageInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
