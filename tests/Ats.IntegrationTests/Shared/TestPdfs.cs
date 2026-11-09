using System.IO.Compression;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Ats.IntegrationTests.Shared;

internal static class TestPdfs
{
    public static byte[] WithText(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText(text, 12, new PdfPoint(72, 720), font);
        return builder.Build();
    }

    // A few kilobytes on disk: one page whose compressed content opens to this many megabytes of text.
    public static byte[] ExpandingTo(int megabytes)
    {
        using var pdf = new MemoryStream();
        var offsets = new List<long>();
        void Write(string part) => pdf.Write(Encoding.ASCII.GetBytes(part));
        void WriteObject(string body)
        {
            offsets.Add(pdf.Position);
            Write(body);
        }

        var content = CompressedTextOps(megabytes);
        Write("%PDF-1.4\n");
        WriteObject("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        WriteObject("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        WriteObject("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R " +
                    "/Resources << /Font << /F1 5 0 R >> >> >> endobj\n");
        WriteObject($"4 0 obj << /Length {content.Length} /Filter /FlateDecode >> stream\n");
        pdf.Write(content);
        Write("\nendstream endobj\n");
        WriteObject("5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n");

        var xref = pdf.Position;
        Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            Write($"{offset:D10} 00000 n \n");
        Write($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return pdf.ToArray();
    }

    private static byte[] CompressedTextOps(int megabytes)
    {
        var line = Encoding.ASCII.GetBytes($"BT /F1 12 Tf 72 720 Td ({new string('A', 1000)}) Tj ET\n");
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            for (long written = 0; written < megabytes * 1024L * 1024; written += line.Length)
                zlib.Write(line);
        }

        return compressed.ToArray();
    }
}
