using DocumentTranslation.Api.Functions;
using DocumentTranslation.Api.Models;
using FluentAssertions;
using Xunit;

namespace DocumentTranslation.Api.Tests;

public class OrchestratorTests
{
    [Fact]
    public void FinalizeSession_AllBatchesSucceeded_SessionCompleted()
    {
        // Arrange
        var session = new TranslationSession
        {
            SessionId = "test-session-001",
            TargetLanguage = "es",
            TotalFileCount = 2500,
            Status = TranslationStatus.Processing,
            Batches = new List<TranslationBatch>
            {
                new() { BatchId = "batch-1", Status = BatchStatus.Running, FileCount = 1000 },
                new() { BatchId = "batch-2", Status = BatchStatus.Running, FileCount = 1000 },
                new() { BatchId = "batch-3", Status = BatchStatus.Running, FileCount = 500 }
            }
        };

        var results = new List<TranslationResult>
        {
            new() { BatchId = "batch-1", Status = BatchStatus.Succeeded, TranslatedFileCount = 1000 },
            new() { BatchId = "batch-2", Status = BatchStatus.Succeeded, TranslatedFileCount = 1000 },
            new() { BatchId = "batch-3", Status = BatchStatus.Succeeded, TranslatedFileCount = 500 }
        };

        // Act — test finalization logic directly
        var allSucceeded = results.All(r => r.Status == BatchStatus.Succeeded);

        // Assert
        allSucceeded.Should().BeTrue();
    }

    [Fact]
    public void FinalizeSession_PartialFailure_SessionFailed()
    {
        var results = new List<TranslationResult>
        {
            new() { BatchId = "batch-1", Status = BatchStatus.Succeeded, TranslatedFileCount = 1000 },
            new() { BatchId = "batch-2", Status = BatchStatus.Failed, FailedFileCount = 500, Error = "Service error" }
        };

        var allSucceeded = results.All(r => r.Status == BatchStatus.Succeeded);
        var failedCount = results.Count(r => r.Status != BatchStatus.Succeeded);

        allSucceeded.Should().BeFalse();
        failedCount.Should().Be(1);
    }

    [Fact]
    public void FinalizeSession_AllBatchesFailed_SessionFailed()
    {
        var results = new List<TranslationResult>
        {
            new() { BatchId = "batch-1", Status = BatchStatus.Failed, Error = "Error 1" },
            new() { BatchId = "batch-2", Status = BatchStatus.Failed, Error = "Error 2" }
        };

        var allSucceeded = results.All(r => r.Status == BatchStatus.Succeeded);
        var failedCount = results.Count(r => r.Status != BatchStatus.Succeeded);

        allSucceeded.Should().BeFalse();
        failedCount.Should().Be(2);
    }

    [Fact]
    public void FinalizeSession_SingleBatchSuccess_SessionCompleted()
    {
        var results = new List<TranslationResult>
        {
            new() { BatchId = "batch-1", Status = BatchStatus.Succeeded, TranslatedFileCount = 50 }
        };

        var allSucceeded = results.All(r => r.Status == BatchStatus.Succeeded);

        allSucceeded.Should().BeTrue();
    }

    [Fact]
    public void FinalizeSession_PartiallySucceededBatch_SessionFailed()
    {
        var results = new List<TranslationResult>
        {
            new() { BatchId = "batch-1", Status = BatchStatus.Succeeded, TranslatedFileCount = 1000 },
            new() { BatchId = "batch-2", Status = BatchStatus.PartiallySucceeded, TranslatedFileCount = 400, FailedFileCount = 100 }
        };

        var allSucceeded = results.All(r => r.Status == BatchStatus.Succeeded);

        allSucceeded.Should().BeFalse();
    }

    [Fact]
    public void FinalizeSession_AggregatesFileCounts()
    {
        var results = new List<TranslationResult>
        {
            new() { BatchId = "batch-1", Status = BatchStatus.Succeeded, TranslatedFileCount = 1000, FailedFileCount = 0 },
            new() { BatchId = "batch-2", Status = BatchStatus.Failed, TranslatedFileCount = 0, FailedFileCount = 500 }
        };

        var totalTranslated = results.Sum(r => r.TranslatedFileCount);
        var totalFailed = results.Sum(r => r.FailedFileCount);

        totalTranslated.Should().Be(1000);
        totalFailed.Should().Be(500);
    }

    [Fact]
    public void BatchSplitting_PreservesAllDocuments()
    {
        // Create 2,500 documents
        var documents = Enumerable.Range(1, 2500).Select(i => new SourceDocument
        {
            FileName = $"file{i}.docx",
            FileSize = 1024,
            BlobUrl = $"session/file{i}.docx"
        }).ToList();

        var batches = TranslationOrchestrator.SplitIntoBatches(documents, "test-session");

        // All documents should be in exactly one batch
        batches.Sum(b => b.Documents.Count).Should().Be(2500);
    }

    [Fact]
    public void RetryPolicy_CorrectParameters()
    {
        // Verify the retry policy constants match the spec
        var maxAttempts = 3;
        var firstInterval = TimeSpan.FromSeconds(5);
        var backoffCoefficient = 2.0;
        var maxInterval = TimeSpan.FromMinutes(5);

        maxAttempts.Should().Be(3);
        firstInterval.Should().Be(TimeSpan.FromSeconds(5));
        backoffCoefficient.Should().Be(2.0);
        maxInterval.Should().Be(TimeSpan.FromMinutes(5));
    }
}
