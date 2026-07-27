using System.IO.Compression;
using System.Text;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.Shared;

// The upload boundary checks a file's leading bytes, not whether the rest of it is readable, so a
// truncated or corrupted CV reaches the extractors intact-looking and fails there. It has to fail
// as TextExtractionException: that is what tells the consumer to skip the file instead of throwing
// something generic that looks transient and gets redelivered five times.
public class CorruptCvFileTests
{
    [Fact]
    public void A_truncated_pdf_should_report_a_text_extraction_failure()
    {
        // Real "%PDF" magic bytes, nothing valid behind them — exactly what got through the
        // boundary and filled the error queue.
        var truncated = Encoding.ASCII.GetBytes("%PDF-1.4 this is not actually a pdf");

        var exception = Assert.Throws<TextExtractionException>(
            () => new PdfPigTextExtractor().Extract(truncated));

        // The library's own exception is kept as the cause: callers must not have to reference
        // PdfPig, but the detail still has to reach the log.
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void A_corrupted_docx_archive_should_report_a_text_extraction_failure()
    {
        var corrupted = Encoding.ASCII.GetBytes("PK broken zip payload");

        var exception = Assert.Throws<TextExtractionException>(
            () => new DocxTextExtractor().Extract(corrupted));

        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void A_zip_that_is_not_a_word_package_should_still_yield_no_text()
    {
        // The counterpart: a readable archive that simply has no document part is not corrupt, and
        // must keep returning empty rather than being reclassified as a failure.
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("not-word/readme.txt");
        }

        var text = new DocxTextExtractor().Extract(buffer.ToArray());

        Assert.Equal(string.Empty, text);
    }
}
