namespace Ats.Shared.Kernel;

public interface IPdfTextExtractor
{
    Task<string> ExtractAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);
}
