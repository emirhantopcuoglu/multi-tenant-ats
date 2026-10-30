namespace Ats.Shared.Kernel;

public interface IPdfTextExtractor
{
    // Synchronous: CPU-bound work on bytes already in memory.
    string Extract(byte[] pdfBytes);
}
