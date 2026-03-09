namespace DocumentTranslation.Api.Models;

public class TranslationBatch
{
    public string BatchId { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Pending;
    public string? TranslationOperationId { get; set; }
    public string SourceBlobPrefix { get; set; } = string.Empty;
    public string TargetBlobPrefix { get; set; } = string.Empty;
    public string? Error { get; set; }

    public static readonly int MaxFilesPerBatch = 1000;
    public static readonly long MaxBytesPerBatch = 250L * 1024 * 1024; // 250 MB

    public List<SourceDocument> Documents { get; set; } = new();
}
