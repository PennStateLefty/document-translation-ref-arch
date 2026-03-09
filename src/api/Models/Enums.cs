namespace DocumentTranslation.Api.Models;

public enum TranslationStatus
{
    Uploading,
    Processing,
    Completed,
    Failed,
    Error
}

public enum BatchStatus
{
    Pending,
    Submitted,
    Running,
    Succeeded,
    PartiallySucceeded,
    Failed,
    Cancelled
}
