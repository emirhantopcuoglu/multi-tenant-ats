using System.Text;
using UglyToad.PdfPig;

namespace Ats.PdfWorker;

public static class PdfText
{
    public static string Extract(byte[] pdfBytes)
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
