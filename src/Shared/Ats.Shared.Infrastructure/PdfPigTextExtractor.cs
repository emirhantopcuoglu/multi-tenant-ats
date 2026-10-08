using System.Text;
using Ats.Shared.Kernel;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace Ats.Shared.Infrastructure;

// PdfPig-backed IPdfTextExtractor. PdfPig is a pure-managed, MIT-licensed PDF reader — no native
// dependencies and no commercial license to track (the reason it was chosen over iText for the
// MVP). Extraction is fully in-memory and CPU-bound, so the interface is synchronous.
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
        // The upload boundary only checks the leading "%PDF" bytes, so a truncated or malformed file
        // reaches this far and fails here — this is the exception that actually filled the CV
        // parsing error queue. Translating it tells the caller the failure is permanent, and keeps
        // PdfPig's own exception types from leaking past this class.
        catch (PdfDocumentFormatException exception)
        {
            throw new TextExtractionException("The PDF is malformed and cannot be read.", exception);
        }
    }
}
