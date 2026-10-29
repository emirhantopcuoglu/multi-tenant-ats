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

            using var partStream = documentPart.Open();
            var document = XDocument.Load(partStream);

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
