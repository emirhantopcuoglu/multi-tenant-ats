using Ats.Shared.Infrastructure;

namespace Ats.UnitTests.Shared;

public class DocxHiddenTextTests
{
    private const string Visible = "Visible";
    private const string Hidden = "Kubernetes";
    private const string VisibleParagraph = $"<w:p><w:r><w:t>{Visible}</w:t></w:r></w:p>";

    [Fact]
    public void text_marked_hidden_should_be_left_out()
    {
        var text = Extract(VisibleParagraph + Paragraph("<w:vanish/>"));

        AssertOnlyVisibleText(text);
    }

    // Word writes this to switch off hidden text that a style turned on.
    [Fact]
    public void text_with_hidden_switched_off_should_be_kept()
    {
        var text = Extract(Paragraph("""<w:vanish w:val="0"/>"""));

        Assert.Contains(Hidden, text);
    }

    // Word's smallest size, 1 pt.
    [Fact]
    public void text_smaller_than_two_points_should_be_left_out()
    {
        var text = Extract(VisibleParagraph + Paragraph("""<w:sz w:val="2"/>"""));

        AssertOnlyVisibleText(text);
    }

    [Fact]
    public void white_text_should_be_left_out()
    {
        var text = Extract(VisibleParagraph + Paragraph("""<w:color w:val="FFFFFF"/>"""));

        AssertOnlyVisibleText(text);
    }

    [Fact]
    public void white_text_in_a_white_cell_should_be_left_out()
    {
        var text = Extract(VisibleParagraph + Cell("FFFFFF", Paragraph("""<w:color w:val="FFFFFF"/>""")));

        AssertOnlyVisibleText(text);
    }

    // The dark sidebar of many CV templates.
    [Fact]
    public void white_text_in_a_dark_cell_should_be_kept()
    {
        var text = Extract(Cell("1E293B", Paragraph("""<w:color w:val="FFFFFF"/>""")));

        Assert.Contains(Hidden, text);
    }

    [Fact]
    public void white_text_on_a_highlight_should_be_kept()
    {
        var text = Extract(Paragraph("""<w:color w:val="FFFFFF"/><w:highlight w:val="black"/>"""));

        Assert.Contains(Hidden, text);
    }

    // Word places a floating shape itself, so there is no telling which text it sits behind.
    [Fact]
    public void white_text_in_a_document_with_a_floating_shape_should_be_kept()
    {
        const string floatingShape =
            """<w:p><w:r><w:drawing><wp:anchor xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" behindDoc="1"/></w:drawing></w:r></w:p>""";

        var text = Extract(floatingShape + Paragraph("""<w:color w:val="FFFFFF"/>"""));

        Assert.Contains(Hidden, text);
    }

    private static string Paragraph(string runProperties) =>
        $"<w:p><w:r><w:rPr>{runProperties}</w:rPr><w:t>{Hidden}</w:t></w:r></w:p>";

    private static string Cell(string fill, string content) =>
        $"""<w:tbl><w:tr><w:tc><w:tcPr><w:shd w:val="clear" w:color="auto" w:fill="{fill}"/></w:tcPr>{content}</w:tc></w:tr></w:tbl>""";

    private static string Extract(string body) => new DocxTextExtractor().Extract(TestDocx.WithBody(body));

    private static void AssertOnlyVisibleText(string text)
    {
        Assert.Contains(Visible, text);
        Assert.DoesNotContain(Hidden, text);
    }
}
