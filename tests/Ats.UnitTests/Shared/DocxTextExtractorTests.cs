using System.Buffers.Binary;
using System.IO.Compression;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.Shared;

public class DocxTextExtractorTests
{
    [Fact]
    public void Extract_should_return_one_line_per_paragraph_with_runs_joined()
    {
        // Arrange: two paragraphs, the first split across two runs (Word fragments runs freely,
        // e.g. around formatting changes, the extractor must stitch them back together).
        var docx = TestDocx.WithBody($"""
            <w:p><w:r><w:t>John App</w:t></w:r><w:r><w:t>licant</w:t></w:r></w:p>
            <w:p><w:r><w:t>Senior Software Engineer</w:t></w:r></w:p>
            """);

        // Act
        var text = new DocxTextExtractor().Extract(docx);

        // Assert
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["John Applicant", "Senior Software Engineer"], lines);
    }

    [Fact]
    public void Extract_should_render_tabs_as_spaces_and_breaks_as_new_lines()
    {
        var docx = TestDocx.WithBody(
            "<w:p><w:r><w:t>Skills:</w:t><w:tab/><w:t>C#</w:t><w:br/><w:t>.NET</w:t></w:r></w:p>");

        var text = new DocxTextExtractor().Extract(docx);

        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Skills: C#", ".NET"], lines);
    }

    [Fact]
    public void Extract_should_include_text_inside_tables()
    {
        // Table cells wrap their content in ordinary <w:p> elements, so a CV laid out as a
        // table (a common template style) must still yield its text.
        var docx = TestDocx.WithBody(
            "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>Istanbul Technical University</w:t></w:r></w:p></w:tc></w:tr></w:tbl>");

        var text = new DocxTextExtractor().Extract(docx);

        Assert.Contains("Istanbul Technical University", text);
    }

    [Fact]
    public void Extract_should_return_empty_when_zip_has_no_document_part()
    {
        // A valid ZIP that is not a Word package passes the upload signature check, so the
        // extractor must yield "no text" rather than throw.
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("readme.txt").Open());
            writer.Write("not a docx");
        }

        var text = new DocxTextExtractor().Extract(stream.ToArray());

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void Extract_should_refuse_a_document_part_that_opens_past_the_size_limit()
    {
        // Compresses to a few kilobytes, which is how a decompression bomb gets past the upload limit.
        var docx = TestDocx.WithBody(ParagraphsOfAtLeast(DocxTextExtractor.MaxDocumentPartBytes + 1));

        Assert.Throws<TextExtractionException>(() => new DocxTextExtractor().Extract(docx));
    }

    [Fact]
    public void Extract_should_not_read_past_a_size_the_archive_understates()
    {
        // The size check trusts the archive header. This pins that the BCL also stops inflating there.
        var docx = TestDocx.WithBody(ParagraphsOfAtLeast(DocxTextExtractor.MaxDocumentPartBytes + 1));
        DeclareDocumentPartSize(docx, 1024);

        Assert.Throws<TextExtractionException>(() => new DocxTextExtractor().Extract(docx));
    }

    [Fact]
    public void Extract_should_refuse_a_document_part_with_a_dtd()
    {
        // Entities would let a tiny part expand past the size limit. Word never writes a DTD.
        var docx = TestDocx.WithDocumentPart($"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE w:document [
              <!ENTITY a "{new string('A', 1000)}">
              <!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">
            ]>
            <w:document xmlns:w="{TestDocx.WordprocessingNamespace}">
              <w:body><w:p><w:r><w:t>{string.Concat(Enumerable.Repeat("&b;", 100))}</w:t></w:r></w:p></w:body>
            </w:document>
            """);

        Assert.Throws<TextExtractionException>(() => new DocxTextExtractor().Extract(docx));
    }

    private static string ParagraphsOfAtLeast(long bytes)
    {
        var paragraph = $"<w:p><w:r><w:t>{new string('A', 1000)}</w:t></w:r></w:p>";
        return string.Concat(Enumerable.Repeat(paragraph, (int)(bytes / paragraph.Length) + 1));
    }

    // Rewrites the uncompressed size in the local header and the central directory of the only entry.
    private static void DeclareDocumentPartSize(byte[] docx, uint size)
    {
        const int LocalHeaderSizeOffset = 22;
        const int CentralDirectorySizeOffset = 24;
        ReadOnlySpan<byte> centralDirectorySignature = [0x50, 0x4B, 0x01, 0x02];

        BinaryPrimitives.WriteUInt32LittleEndian(docx.AsSpan(LocalHeaderSizeOffset), size);
        var centralDirectory = docx.AsSpan().IndexOf(centralDirectorySignature);
        BinaryPrimitives.WriteUInt32LittleEndian(docx.AsSpan(centralDirectory + CentralDirectorySizeOffset), size);
    }
}
