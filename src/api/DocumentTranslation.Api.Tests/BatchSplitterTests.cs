using DocumentTranslation.Api.Functions;
using DocumentTranslation.Api.Models;
using FluentAssertions;
using Xunit;

namespace DocumentTranslation.Api.Tests;

public class BatchSplitterTests
{
    private const string TestSessionId = "test-session-001";

    private static List<SourceDocument> CreateDocuments(int count, long fileSizeBytes = 1024)
    {
        return Enumerable.Range(1, count).Select(i => new SourceDocument
        {
            FileName = $"file{i}.docx",
            FileSize = fileSizeBytes,
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BlobUrl = $"{TestSessionId}/file{i}.docx"
        }).ToList();
    }

    [Fact]
    public void UnderLimits_SingleBatch()
    {
        // Arrange: 500 files, each 1 KB = well under both limits
        var docs = CreateDocuments(500, 1024);

        // Act
        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        // Assert
        batches.Should().HaveCount(1);
        batches[0].FileCount.Should().Be(500);
        batches[0].TotalSize.Should().Be(500 * 1024L);
    }

    [Fact]
    public void ExactlyAtFileCountLimit_SingleBatch()
    {
        // Arrange: exactly 1,000 files (at the limit, not over)
        var docs = CreateDocuments(1000, 1024);

        // Act
        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        // Assert
        batches.Should().HaveCount(1);
        batches[0].FileCount.Should().Be(1000);
    }

    [Fact]
    public void ExactlyAtSizeLimit_SingleBatch()
    {
        // Arrange: 250 files each exactly 1 MB = exactly 250 MB
        var docs = CreateDocuments(250, 1024 * 1024);

        // Act
        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        // Assert
        batches.Should().HaveCount(1);
        batches[0].FileCount.Should().Be(250);
        batches[0].TotalSize.Should().Be(250L * 1024 * 1024);
    }

    [Fact]
    public void OverFileCountLimit_SplitsIntoBatches()
    {
        // Arrange: 2,500 files (should split into 3 batches: 1000 + 1000 + 500)
        var docs = CreateDocuments(2500, 1024);

        // Act
        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        // Assert
        batches.Should().HaveCount(3);
        batches[0].FileCount.Should().Be(1000);
        batches[1].FileCount.Should().Be(1000);
        batches[2].FileCount.Should().Be(500);
    }

    [Fact]
    public void OverSizeLimit_SplitsIntoBatches()
    {
        // Arrange: 10 files each 50 MB = 500 MB total (should split into 2 batches of 5)
        var fileSizeBytes = 50L * 1024 * 1024;
        var docs = CreateDocuments(10, fileSizeBytes);

        // Act
        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        // Assert
        batches.Should().HaveCount(2);
        batches[0].FileCount.Should().Be(5);
        batches[0].TotalSize.Should().Be(5 * fileSizeBytes);
        batches[1].FileCount.Should().Be(5);
        batches[1].TotalSize.Should().Be(5 * fileSizeBytes);
    }

    [Fact]
    public void BothLimitsExceeded_UsesMoreRestrictiveLimit()
    {
        // Arrange: 3,000 files each 100 KB = ~293 MB
        // File count limit hits first (1000), but size also matters
        var docs = CreateDocuments(3000, 100 * 1024);

        // Act
        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        // Assert
        batches.Should().HaveCountGreaterOrEqualTo(3);
        batches.All(b => b.FileCount <= TranslationBatch.MaxFilesPerBatch).Should().BeTrue();
        batches.All(b => b.TotalSize <= TranslationBatch.MaxBytesPerBatch).Should().BeTrue();
    }

    [Fact]
    public void Exactly1001Files_SplitsIntoTwoBatches()
    {
        var docs = CreateDocuments(1001, 1024);

        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        batches.Should().HaveCount(2);
        batches[0].FileCount.Should().Be(1000);
        batches[1].FileCount.Should().Be(1);
    }

    [Fact]
    public void AllBatches_HaveCorrectSessionId()
    {
        var docs = CreateDocuments(2500, 1024);

        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        batches.Should().AllSatisfy(b => b.SessionId.Should().Be(TestSessionId));
    }

    [Fact]
    public void AllBatches_HaveUniqueBatchIds()
    {
        var docs = CreateDocuments(2500, 1024);

        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        batches.Select(b => b.BatchId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SingleFile_SingleBatch()
    {
        var docs = CreateDocuments(1, 1024);

        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        batches.Should().HaveCount(1);
        batches[0].FileCount.Should().Be(1);
    }

    [Fact]
    public void EmptyList_ReturnsEmptyBatches()
    {
        var batches = TranslationOrchestrator.SplitIntoBatches(new List<SourceDocument>(), TestSessionId);

        batches.Should().BeEmpty();
    }

    [Fact]
    public void AllBatches_RespectBothLimitsSimultaneously()
    {
        // Arrange: Mix of large and small files
        var docs = new List<SourceDocument>();
        // 500 files at 200 KB each
        docs.AddRange(CreateDocuments(500, 200 * 1024));
        // 600 files at 300 KB each  
        docs.AddRange(Enumerable.Range(501, 600).Select(i => new SourceDocument
        {
            FileName = $"file{i}.pdf",
            FileSize = 300 * 1024,
            ContentType = "application/pdf",
            BlobUrl = $"{TestSessionId}/file{i}.pdf"
        }));

        var batches = TranslationOrchestrator.SplitIntoBatches(docs, TestSessionId);

        // Verify all batches respect both limits
        foreach (var batch in batches)
        {
            batch.FileCount.Should().BeLessOrEqualTo(TranslationBatch.MaxFilesPerBatch,
                $"batch {batch.BatchId} exceeds file count limit");
            batch.TotalSize.Should().BeLessOrEqualTo(TranslationBatch.MaxBytesPerBatch,
                $"batch {batch.BatchId} exceeds size limit");
        }

        // Verify all files are accounted for
        batches.Sum(b => b.FileCount).Should().Be(1100);
    }
}
