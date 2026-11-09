using System.Text;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.IntegrationTests.Shared;

// Runs the real worker process.
public class PdfWorkerTextExtractorTests
{
    [Fact]
    public async Task a_readable_pdf_should_come_back_as_text()
    {
        var pdf = TestPdfs.WithText("Backend Developer");

        var text = await new PdfWorkerTextExtractor().ExtractAsync(pdf);

        Assert.Contains("Backend Developer", text);
    }

    [Fact]
    public async Task a_pdf_that_needs_more_memory_than_the_limit_should_be_refused()
    {
        var bomb = TestPdfs.ExpandingTo(megabytes: 4);

        var exception = await Assert.ThrowsAsync<TextExtractionException>(
            () => new PdfWorkerTextExtractor().ExtractAsync(bomb));

        Assert.Contains($"{PdfWorkerTextExtractor.MemoryLimitMegabytes} MB", exception.Message);
    }

    [Fact]
    public async Task a_pdf_that_takes_longer_than_the_time_limit_should_be_refused()
    {
        var pdf = TestPdfs.WithText("Backend Developer");

        var exception = await Assert.ThrowsAsync<TextExtractionException>(
            () => new PdfWorkerTextExtractor(timeLimit: TimeSpan.FromMilliseconds(1)).ExtractAsync(pdf));

        Assert.Contains("longer than", exception.Message);
    }

    // A shutdown is not a broken file: the message has to stay retryable.
    [Fact]
    public async Task a_cancelled_read_should_not_be_reported_as_an_unreadable_pdf()
    {
        var pdf = TestPdfs.WithText("Backend Developer");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new PdfWorkerTextExtractor().ExtractAsync(pdf, new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task a_malformed_pdf_should_be_refused_with_the_reason()
    {
        var truncated = Encoding.ASCII.GetBytes("%PDF-1.4 this is not actually a pdf");

        var exception = await Assert.ThrowsAsync<TextExtractionException>(
            () => new PdfWorkerTextExtractor().ExtractAsync(truncated));

        Assert.StartsWith("The PDF is malformed: ", exception.Message);
        Assert.True(exception.Message.Length > "The PDF is malformed: ".Length);
    }
}
