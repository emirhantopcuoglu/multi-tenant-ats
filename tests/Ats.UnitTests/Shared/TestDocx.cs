using System.IO.Compression;
using System.Text;

namespace Ats.UnitTests.Shared;

internal static class TestDocx
{
    public const string WordprocessingNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static byte[] WithBody(string bodyXml)
    {
        return WithDocumentPart($"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="{WordprocessingNamespace}">
              <w:body>{bodyXml}</w:body>
            </w:document>
            """);
    }

    public static byte[] WithDocumentPart(string documentXml)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(
                archive.CreateEntry("word/document.xml").Open(), Encoding.UTF8);
            writer.Write(documentXml);
        }

        return stream.ToArray();
    }
}
