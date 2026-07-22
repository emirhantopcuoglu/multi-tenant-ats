using System.IO.Compression;
using System.Text;
using Ats.Shared.Infrastructure;

namespace Ats.UnitTests.Shared;

public class DocxTextExtractorTests
{
    private const string WordprocessingNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    [Fact]
    public void Extract_should_return_one_line_per_paragraph_with_runs_joined()
    {
        // Arrange: two paragraphs, the first split across two runs (Word fragments runs freely,
        // e.g. around formatting changes — the extractor must stitch them back together).
        var docx = BuildDocx($"""
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
        var docx = BuildDocx(
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
        var docx = BuildDocx(
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

    private static byte[] BuildDocx(string bodyXml)
    {
        var documentXml = $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="{WordprocessingNamespace}">
              <w:body>{bodyXml}</w:body>
            </w:document>
            """;

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(
                archive.CreateEntry("word/document.xml").Open(), Encoding.UTF8);
            writer.Write(documentXml);
        }

        return stream.ToArray();
    }
}
