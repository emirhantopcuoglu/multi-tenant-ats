using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Ats.Shared.Kernel;

namespace Ats.Shared.Infrastructure;

// IDocxTextExtractor backed by the BCL alone. A DOCX is a ZIP package whose visible text lives in
// word/document.xml as <w:t> runs, so System.IO.Compression + XDocument cover the whole job — a
// dedicated OOXML library would add a dependency for no extra capability here (we only ever need
// plain text for the LLM, never styling or layout).
public sealed class DocxTextExtractor : IDocxTextExtractor
{
    private const string DocumentPartPath = "word/document.xml";

    // WordprocessingML main namespace — every text-bearing element in document.xml lives in it.
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public string Extract(byte[] docxBytes)
    {
        using var stream = new MemoryStream(docxBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        // A ZIP that is not a Word package (no document part) yields no text rather than an
        // exception: the upload boundary only checks the ZIP signature, so this input is
        // reachable and simply has nothing for us to extract.
        var documentPart = archive.GetEntry(DocumentPartPath);
        if (documentPart is null)
            return string.Empty;

        using var partStream = documentPart.Open();
        var document = XDocument.Load(partStream);

        return ExtractParagraphText(document);
    }

    private static string ExtractParagraphText(XDocument document)
    {
        var builder = new StringBuilder();

        // One output line per <w:p> keeps the paragraph structure the author saw, which is the
        // only layout signal worth preserving for the LLM. Table cells contain their own <w:p>
        // elements, so tables flatten to lines without special handling.
        foreach (var paragraph in document.Descendants(W + "p"))
        {
            foreach (var node in paragraph.Descendants())
            {
                if (node.Name == W + "t")
                    builder.Append(node.Value);
                else if (node.Name == W + "tab")
                    builder.Append(' ');
                else if (node.Name == W + "br" || node.Name == W + "cr")
                    builder.AppendLine();
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}
