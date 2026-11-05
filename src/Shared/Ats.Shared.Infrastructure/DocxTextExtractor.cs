using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ats.Shared.Kernel;

namespace Ats.Shared.Infrastructure;

// A DOCX is a ZIP with the text in word/document.xml, so the BCL is enough; only plain text is needed.
public sealed class DocxTextExtractor : IDocxTextExtractor
{
    private const string DocumentPartPath = "word/document.xml";

    // A CV's document.xml is tens of kilobytes; this is already a long book.
    public const long MaxDocumentPartBytes = 5 * 1024 * 1024;

    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public string Extract(byte[] docxBytes)
    {
        try
        {
            using var stream = new MemoryStream(docxBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            // A ZIP that isn't a Word document yields no text instead of an exception.
            var documentPart = archive.GetEntry(DocumentPartPath);
            if (documentPart is null)
                return string.Empty;

            // Length comes from the archive header. The BCL never inflates past it, so this bounds
            // memory even when the header lies, and a bomb is refused before anything is inflated.
            if (documentPart.Length > MaxDocumentPartBytes)
                throw new TextExtractionException(
                    $"The DOCX document part opens to {documentPart.Length} bytes, over the {MaxDocumentPartBytes} limit.");

            using var partStream = documentPart.Open();
            using var reader = XmlReader.Create(partStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var document = XDocument.Load(reader);

            return ExtractParagraphText(document);
        }
        // A broken archive can't be fixed by a retry, so report it as TextExtractionException.
        catch (InvalidDataException exception)
        {
            throw new TextExtractionException("The DOCX archive is corrupted.", exception);
        }
        catch (XmlException exception)
        {
            throw new TextExtractionException("The DOCX document part is not valid XML.", exception);
        }
    }

    private static string ExtractParagraphText(XDocument document)
    {
        var builder = new StringBuilder();

        // One line per paragraph. Table cells contain paragraphs, so tables need no special handling.
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
