using System.Globalization;
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

    // 2 pt; w:sz is in half-points, and Word's smallest size is 1 pt.
    private const int MinReadableHalfPoints = 4;
    // #F2F2F2 and lighter, as for PDFs.
    private const int NearWhiteChannel = 0xF2;

    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Wp =
        "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XName[] PropertyElements = [W + "rPr", W + "pPr", W + "tcPr", W + "tblPr"];

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
        var hasFloatingContent = HasFloatingContent(document);

        // One line per paragraph. Table cells contain paragraphs, so tables need no special handling.
        foreach (var paragraph in document.Descendants(W + "p"))
        {
            foreach (var node in paragraph.Descendants())
            {
                if (node.Parent!.Name == W + "r" && IsHidden(node.Parent, hasFloatingContent))
                    continue;

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

    // Text a reader can't see is left out, as for PDFs. Only direct formatting is read, not styles.
    private static bool IsHidden(XElement run, bool hasFloatingContent)
    {
        var properties = run.Element(W + "rPr");
        if (properties is null)
            return false;

        return IsSwitchedOn(properties.Element(W + "vanish"))
            || HalfPoints(properties.Element(W + "sz")) < MinReadableHalfPoints
            || (IsNearWhite(ValueOf(properties.Element(W + "color"))) && !hasFloatingContent && !IsShaded(run));
    }

    // Word places floating shapes, text boxes and the page colour itself, so any text could sit on one.
    private static bool HasFloatingContent(XDocument document) =>
        document.Descendants(Wp + "anchor").Any()
        || document.Descendants(W + "pict").Any()
        || document.Descendants(W + "background").Any();

    // Shading or a highlight on the run, or shading on the paragraph, cell or table around it.
    private static bool IsShaded(XElement run) =>
        run.AncestorsAndSelf()
            .SelectMany(element => PropertyElements.Select(element.Element).OfType<XElement>())
            .Any(properties => IsBackground(properties.Element(W + "shd"))
                || ValueOf(properties.Element(W + "highlight")) is not (null or "none" or "white"));

    // Word's shading button writes a fill colour; a pattern counts as well.
    private static bool IsBackground(XElement? shading) =>
        shading is not null
        && (ValueOf(shading) is not (null or "clear" or "nil")
            || ((string?)shading.Attribute(W + "fill") is { } fill && fill != "auto" && !IsNearWhite(fill)));

    private static bool IsSwitchedOn(XElement? toggle) =>
        toggle is not null && ValueOf(toggle) is (null or "1" or "true" or "on");

    private static int HalfPoints(XElement? size) =>
        int.TryParse(ValueOf(size), NumberStyles.None, CultureInfo.InvariantCulture, out var halfPoints)
            ? halfPoints
            : int.MaxValue;

    private static bool IsNearWhite(string? hex) =>
        hex is { Length: 6 }
        && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
        && (rgb >> 16) >= NearWhiteChannel
        && ((rgb >> 8) & 0xFF) >= NearWhiteChannel
        && (rgb & 0xFF) >= NearWhiteChannel;

    private static string? ValueOf(XElement? element) => (string?)element?.Attribute(W + "val");
}
