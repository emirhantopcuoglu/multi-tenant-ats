namespace Ats.Shared.Kernel;

// Checks the file's leading bytes. The extension and Content-Type are client-controlled, so they
// are never trusted. In the Kernel so both controllers and handlers can use it.
public static class FileSignatureValidator
{
    public static readonly Error Empty = new("file.empty", "The file is empty.");
    public static readonly Error TooLarge = new("file.too_large", "The file exceeds the maximum allowed size.");
    public static readonly Error UnsupportedType = new("file.unsupported_type", "Only PDF and DOCX files are accepted.");
    public static readonly Error ContentMismatch = new("file.content_mismatch", "The file content does not match its declared type.");

    private const string PdfContentType = "application/pdf";
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46];

    // ZIP signature. Any ZIP passes; telling a real DOCX apart would need package inspection.
    private static readonly byte[] DocxSignature = [0x50, 0x4B, 0x03, 0x04];

    private const int SignatureProbeBytes = 8;

    // Reads and rewinds the stream itself, so callers can't forget the rewind.
    public static async Task<Result> ValidateAsync(
        Stream content, string contentType, long sizeBytes, long maxSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var header = new byte[SignatureProbeBytes];
        var bytesRead = await content.ReadAsync(header, cancellationToken);

        var result = Validate(header.AsSpan(0, bytesRead), contentType, sizeBytes, maxSizeBytes);

        if (content.CanSeek)
            content.Position = 0;

        return result;
    }

    public static Result Validate(
        ReadOnlySpan<byte> header, string contentType, long sizeBytes, long maxSizeBytes)
    {
        if (sizeBytes <= 0)
            return Result.Failure(Empty);

        if (sizeBytes > maxSizeBytes)
            return Result.Failure(TooLarge);

        var expectedSignature = contentType switch
        {
            PdfContentType => PdfSignature,
            DocxContentType => DocxSignature,
            _ => null
        };

        if (expectedSignature is null)
            return Result.Failure(UnsupportedType);

        return header.StartsWith(expectedSignature)
            ? Result.Success()
            : Result.Failure(ContentMismatch);
    }
}
