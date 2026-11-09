using System.Text;
using Ats.PdfWorker;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Ats.UnitTests.Shared;

public class PdfHiddenTextTests
{
    private const string Visible = "Visible";
    private const string Hidden = "Kubernetes";

    // A 1x1 dark navy PNG.
    private static readonly byte[] DarkPixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGMQkbMBAAC4AG8CTG4CAAAAAElFTkSuQmCC");

    [Fact]
    public void invisible_text_should_be_left_out()
    {
        var text = ExtractPage((page, font) =>
        {
            page.SetTextRenderingMode(TextRenderingMode.Neither);
            page.AddText(Hidden, 12, new PdfPoint(72, 700), font);
        });

        AssertOnlyVisibleText(text);
    }

    [Fact]
    public void text_smaller_than_two_points_should_be_left_out()
    {
        var text = ExtractPage((page, font) => page.AddText(Hidden, 1.5, new PdfPoint(72, 700), font));

        AssertOnlyVisibleText(text);
    }

    [Fact]
    public void text_outside_the_page_should_be_left_out()
    {
        var text = ExtractPage((page, font) => page.AddText(Hidden, 12, new PdfPoint(-500, 700), font));

        AssertOnlyVisibleText(text);
    }

    [Fact]
    public void white_text_with_nothing_behind_it_should_be_left_out()
    {
        var text = ExtractPage((page, font) =>
        {
            page.SetTextAndFillColor(255, 255, 255);
            page.AddText(Hidden, 12, new PdfPoint(72, 700), font);
        });

        AssertOnlyVisibleText(text);
    }

    [Fact]
    public void white_text_on_a_white_shape_should_be_left_out()
    {
        var text = ExtractPage((page, font) =>
        {
            page.SetTextAndFillColor(255, 255, 255);
            page.DrawRectangle(new PdfPoint(60, 680), 200, 50, 1, fill: true);
            page.AddText(Hidden, 12, new PdfPoint(72, 700), font);
        });

        AssertOnlyVisibleText(text);
    }

    // The dark sidebar of many CV templates.
    [Fact]
    public void white_text_on_a_dark_shape_should_be_kept()
    {
        var text = ExtractPage((page, font) =>
        {
            page.SetTextAndFillColor(20, 30, 60);
            page.DrawRectangle(new PdfPoint(60, 680), 200, 50, 1, fill: true);
            page.SetTextAndFillColor(255, 255, 255);
            page.AddText(Hidden, 12, new PdfPoint(72, 700), font);
        });

        Assert.Contains(Hidden, text);
    }

    [Fact]
    public void white_text_on_an_image_should_be_kept()
    {
        var text = ExtractPage((page, font) =>
        {
            page.AddPng(DarkPixel, new PdfRectangle(60, 680, 260, 730));
            page.SetTextAndFillColor(255, 255, 255);
            page.AddText(Hidden, 12, new PdfPoint(72, 700), font);
        });

        Assert.Contains(Hidden, text);
    }

    // A pattern has no single colour and PdfPig throws when asked for one, which would crash the worker.
    [Fact]
    public void white_text_on_a_gradient_should_be_kept()
    {
        const string gradient =
            "<< /Pattern << /P1 << /PatternType 2 /Shading << /ShadingType 2 /ColorSpace /DeviceRGB " +
            "/Coords [60 0 260 0] /Function << /FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [0 0 0.5] /N 1 >> " +
            ">> >> >> /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >>";
        const string content =
            $"/Pattern cs /P1 scn 60 680 200 50 re f BT /F1 12 Tf 1 1 1 rg 72 700 Td ({Hidden}) Tj ET";

        var text = PdfText.Extract(SinglePagePdf(gradient, content));

        Assert.Contains(Hidden, text);
    }

    private static string ExtractPage(Action<PdfPageBuilder, PdfDocumentBuilder.AddedFont> draw)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(Visible, 12, new PdfPoint(72, 760), font);
        draw(page, font);
        return PdfText.Extract(builder.Build());
    }

    private static void AssertOnlyVisibleText(string text)
    {
        Assert.Contains(Visible, text);
        Assert.DoesNotContain(Hidden, text);
    }

    private static byte[] SinglePagePdf(string resources, string content)
    {
        using var pdf = new MemoryStream();
        var offsets = new List<long>();
        void Write(string part) => pdf.Write(Encoding.ASCII.GetBytes(part));
        void WriteObject(string body)
        {
            offsets.Add(pdf.Position);
            Write(body);
        }

        Write("%PDF-1.4\n");
        WriteObject("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        WriteObject("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        WriteObject($"3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources {resources} >> endobj\n");
        WriteObject($"4 0 obj << /Length {content.Length} >> stream\n{content}\nendstream endobj\n");

        var xref = pdf.Position;
        Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            Write($"{offset:D10} 00000 n \n");
        Write($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return pdf.ToArray();
    }
}
