using System.Text;
using Ats.Shared.Kernel;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace Ats.Shared.Infrastructure;

// PdfPig: managed, MIT-licensed, no native dependencies. CPU-bound, so synchronous.
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public string Extract(byte[] pdfBytes)
    {
        try
        {
            using var document = PdfDocument.Open(pdfBytes);

            var builder = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                builder.AppendLine(page.Text);
            }

            return builder.ToString();
        }
        // A malformed PDF fails here. Translated so the caller knows it is permanent and PdfPig types
        // don't leak out.
        catch (PdfDocumentFormatException exception)
        {
            throw new TextExtractionException("The PDF is malformed and cannot be read.", exception);
        }
    }
}
