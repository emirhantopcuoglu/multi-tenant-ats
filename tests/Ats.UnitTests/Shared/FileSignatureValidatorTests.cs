using Ats.Shared.Kernel;

namespace Ats.UnitTests.Shared;

public class FileSignatureValidatorTests
{
    private const long MaxSize = 10 * 1024 * 1024;
    private const string PdfContentType = "application/pdf";
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly byte[] PdfHeader = [0x25, 0x50, 0x44, 0x46, 0x2D]; // %PDF-
    private static readonly byte[] ZipHeader = [0x50, 0x4B, 0x03, 0x04, 0x14]; // PK..

    [Fact]
    public void Validate_should_accept_a_real_pdf()
    {
        var result = FileSignatureValidator.Validate(PdfHeader, PdfContentType, sizeBytes: 1024, MaxSize);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_should_accept_a_real_docx()
    {
        var result = FileSignatureValidator.Validate(ZipHeader, DocxContentType, sizeBytes: 1024, MaxSize);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_should_reject_an_executable_renamed_to_pdf()
    {
        // Arrange: MZ is the DOS/PE header — an .exe disguised with a .pdf name and
        // a forged "application/pdf" Content-Type.
        byte[] executableHeader = [0x4D, 0x5A, 0x90, 0x00];

        // Act
        var result = FileSignatureValidator.Validate(executableHeader, PdfContentType, sizeBytes: 1024, MaxSize);

        // Assert: the declared type passes the whitelist, but the bytes betray it.
        Assert.True(result.IsFailure);
        Assert.Equal(FileSignatureValidator.ContentMismatch, result.Error);
    }

    [Fact]
    public void Validate_should_reject_an_unsupported_content_type()
    {
        var result = FileSignatureValidator.Validate(PdfHeader, "image/png", sizeBytes: 1024, MaxSize);

        Assert.True(result.IsFailure);
        Assert.Equal(FileSignatureValidator.UnsupportedType, result.Error);
    }

    [Fact]
    public void Validate_should_reject_an_empty_file()
    {
        var result = FileSignatureValidator.Validate(PdfHeader, PdfContentType, sizeBytes: 0, MaxSize);

        Assert.True(result.IsFailure);
        Assert.Equal(FileSignatureValidator.Empty, result.Error);
    }

    [Fact]
    public void Validate_should_reject_a_file_over_the_size_limit()
    {
        var result = FileSignatureValidator.Validate(PdfHeader, PdfContentType, sizeBytes: MaxSize + 1, MaxSize);

        Assert.True(result.IsFailure);
        Assert.Equal(FileSignatureValidator.TooLarge, result.Error);
    }
}
