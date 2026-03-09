namespace DocumentTranslation.Api.Models;

public class SourceDocument
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;

    public static readonly long MaxFileSize = 100 * 1024 * 1024; // 100 MB

    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".html", ".htm", ".txt", ".xlf", ".xliff", ".tsv"
    };

    public static bool IsSupportedExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) && SupportedExtensions.Contains(extension);
    }
}
