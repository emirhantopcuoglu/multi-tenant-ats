namespace Ats.Shared.Kernel;

// Separate from IPdfTextExtractor; the CV consumer picks one by magic bytes.
public interface IDocxTextExtractor
{
    // Synchronous: CPU-bound work on bytes already in memory. Empty when there's no document part.
    string Extract(byte[] docxBytes);
}
