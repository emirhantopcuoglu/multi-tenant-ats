using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Colors;

namespace Ats.PdfWorker;

// Text a reader of the CV can't see is left out, so hidden keywords or instructions never reach the LLM.
public static class PdfText
{
    private const double MinReadablePointSize = 1;
    // #F2F2F2 and lighter.
    private const double NearWhite = 0.95;

    public static string Extract(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);

        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            var backgrounds = BackgroundsOf(page);
            foreach (var letter in page.Letters)
            {
                if (IsVisible(letter, page.CropBox.Bounds, backgrounds))
                    builder.Append(letter.Value);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static bool IsVisible(Letter letter, PdfRectangle pageArea, List<PdfRectangle> backgrounds) =>
        letter.RenderingMode is not (TextRenderingMode.Neither or TextRenderingMode.NeitherClip)
        && letter.PointSize >= MinReadablePointSize
        && Contains(pageArea, letter.StartBaseLine)
        && (!IsNearWhite(letter.Color) || backgrounds.Any(area => Contains(area, letter.StartBaseLine)));

    // What a white letter can sit on and still be seen, such as a dark sidebar.
    private static List<PdfRectangle> BackgroundsOf(Page page) =>
        page.Paths
            .Where(path => path.IsFilled && !IsNearWhite(path.FillColor))
            .Select(path => path.GetBoundingRectangle())
            .OfType<PdfRectangle>()
            .Concat(page.GetImages().Select(image => image.BoundingBox))
            .ToList();

    // A pattern, such as a gradient, has no single colour, and PdfPig throws if asked for one.
    private static bool IsNearWhite(IColor? color)
    {
        if (color is null or PatternColor)
            return false;

        var (red, green, blue) = color.ToRGBValues();
        return red >= NearWhite && green >= NearWhite && blue >= NearWhite;
    }

    private static bool Contains(PdfRectangle area, PdfPoint point) =>
        point.X >= area.Left && point.X <= area.Right && point.Y >= area.Bottom && point.Y <= area.Top;
}
