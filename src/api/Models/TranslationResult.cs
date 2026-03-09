namespace DocumentTranslation.Api.Models;

public class TranslationResult
{
    public string BatchId { get; set; } = string.Empty;
    public BatchStatus Status { get; set; }
    public int TranslatedFileCount { get; set; }
    public int FailedFileCount { get; set; }
    public string? Error { get; set; }
}
