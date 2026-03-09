namespace DocumentTranslation.Api.Models;

public class TranslationSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string TargetLanguage { get; set; } = string.Empty;
    public TranslationStatus Status { get; set; } = TranslationStatus.Uploading;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int TotalFileCount { get; set; }
    public long TotalFileSize { get; set; }
    public List<TranslationBatch> Batches { get; set; } = new();
    public string? Error { get; set; }
}
