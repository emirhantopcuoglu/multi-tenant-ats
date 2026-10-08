namespace Ats.Shared.Kernel;

// Validates an uploaded file at the boundary, before a single byte reaches storage.
//
// Why magic bytes and not the file extension or the client's Content-Type header? Both are
// attacker-controlled: renaming evil.exe to cv.pdf, or sending Content-Type: application/pdf
// for arbitrary bytes, costs nothing. The leading bytes of the actual content ("magic bytes")
// reveal the real format. This is the OWASP defence against unrestricted file upload.
//
// Lives in the Kernel rather than Infrastructure: it is a pure, allocation-free, dependency-free
// rule (it only needs Result/Error), so both the API boundary and any handler can reuse it
// without taking an infrastructure dependency.
public static class FileSignatureValidator
{
    public static readonly Error Empty = new("file.empty", "The file is empty.");
    public static readonly Error TooLarge = new("file.too_large", "The file exceeds the maximum allowed size.");
    public static readonly Error UnsupportedType = new("file.unsupported_type", "Only PDF and DOCX files are accepted.");
    public static readonly Error ContentMismatch = new("file.content_mismatch", "The file content does not match its declared type.");

    private const string PdfContentType = "application/pdf";
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // %PDF
    private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46];

    // PK\x03\x04 — DOCX is an OOXML package, i.e. a ZIP archive. Note this signature is shared
    // by every ZIP-based format; distinguishing a real DOCX from any ZIP needs OOXML package
    // inspection, which is out of scope for the MVP whitelist.
    private static readonly byte[] DocxSignature = [0x50, 0x4B, 0x03, 0x04];

    // Enough leading bytes to cover every whitelisted signature (PDF and DOCX are 4 each).
    private const int SignatureProbeBytes = 8;

    // Reads the leading bytes itself and rewinds, so callers with a stream in hand do not each
    // reimplement the probe-and-rewind dance — forgetting the rewind uploads a file with its first
    // bytes missing, which no test of the validator itself would catch.
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
