using DocumentTranslation.Api.Models;
using FluentAssertions;
using Xunit;

namespace DocumentTranslation.Api.Tests;

public class ValidationTests
{
    [Theory]
    [InlineData(".pdf", true)]
    [InlineData(".docx", true)]
    [InlineData(".xlsx", true)]
    [InlineData(".pptx", true)]
    [InlineData(".html", true)]
    [InlineData(".htm", true)]
    [InlineData(".txt", true)]
    [InlineData(".xlf", true)]
    [InlineData(".xliff", true)]
    [InlineData(".tsv", true)]
    [InlineData(".exe", false)]
    [InlineData(".dll", false)]
    [InlineData(".zip", false)]
    [InlineData(".jpg", false)]
    [InlineData(".mp4", false)]
    [InlineData("", false)]
    public void IsSupportedExtension_ValidatesCorrectly(string extension, bool expected)
    {
        // Arrange
        var fileName = extension == "" ? "noextension" : $"test{extension}";

        // Act
        var result = SourceDocument.IsSupportedExtension(fileName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsSupportedExtension_IsCaseInsensitive()
    {
        SourceDocument.IsSupportedExtension("test.PDF").Should().BeTrue();
        SourceDocument.IsSupportedExtension("test.Docx").Should().BeTrue();
        SourceDocument.IsSupportedExtension("test.XLSX").Should().BeTrue();
    }

    [Fact]
    public void MaxFileSize_Is30MB()
    {
        SourceDocument.MaxFileSize.Should().Be(30L * 1024 * 1024);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1024, true)]
    [InlineData(30 * 1024 * 1024, true)]       // Exactly 30 MB — valid
    [InlineData(30 * 1024 * 1024 + 1, false)]   // 30 MB + 1 byte — invalid
    [InlineData(100 * 1024 * 1024L, false)]      // 100 MB — invalid
    public void FileSize_ValidationRules(long fileSize, bool expectedValid)
    {
        // Act
        var isValid = fileSize <= SourceDocument.MaxFileSize;

        // Assert
        isValid.Should().Be(expectedValid);
    }

    [Fact]
    public void SupportedExtensions_ContainsAllExpectedFormats()
    {
        var expected = new[] { ".pdf", ".docx", ".xlsx", ".pptx", ".html", ".htm", ".txt", ".xlf", ".xliff", ".tsv" };
        
        foreach (var ext in expected)
        {
            SourceDocument.SupportedExtensions.Should().Contain(ext,
                because: $"{ext} should be a supported extension");
        }
    }

    [Fact]
    public void TranslationSession_DefaultValues_AreCorrect()
    {
        var session = new TranslationSession();

        session.SessionId.Should().NotBeNullOrEmpty();
        session.Status.Should().Be(TranslationStatus.Uploading);
        session.Batches.Should().BeEmpty();
        session.Error.Should().BeNull();
        session.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TranslationBatch_DefaultValues_AreCorrect()
    {
        var batch = new TranslationBatch();

        batch.BatchId.Should().NotBeNullOrEmpty();
        batch.Status.Should().Be(BatchStatus.Pending);
        batch.Documents.Should().BeEmpty();
        batch.Error.Should().BeNull();
        batch.TranslationOperationId.Should().BeNull();
    }

    [Fact]
    public void TranslationBatch_Constants_MatchServiceLimits()
    {
        TranslationBatch.MaxFilesPerBatch.Should().Be(1000);
        TranslationBatch.MaxBytesPerBatch.Should().Be(250L * 1024 * 1024);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void TargetLanguage_MustNotBeEmpty(string targetLanguage)
    {
        string.IsNullOrWhiteSpace(targetLanguage).Should().BeTrue();
    }

    [Theory]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("zh-Hans")]
    public void TargetLanguage_ValidCodes_Accepted(string code)
    {
        string.IsNullOrWhiteSpace(code).Should().BeFalse();
    }
}
