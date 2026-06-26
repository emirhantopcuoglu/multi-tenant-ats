using System.Text;
using Ats.Shared.Kernel;
using UglyToad.PdfPig;

namespace Ats.Shared.Infrastructure;

// PdfPig-backed IPdfTextExtractor. PdfPig is a pure-managed, MIT-licensed PDF reader — no native
// dependencies and no commercial license to track (the reason it was chosen over iText for the
// MVP). Extraction is fully in-memory and CPU-bound, so the interface is synchronous.
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public string Extract(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);

        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }
}
