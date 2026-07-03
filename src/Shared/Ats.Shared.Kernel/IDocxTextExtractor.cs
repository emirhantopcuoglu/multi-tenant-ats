namespace Ats.Shared.Kernel;

// Port over "turn a DOCX's bytes into plain text" — the OOXML counterpart of IPdfTextExtractor.
// Kept as a separate port rather than one format-sniffing interface so each implementation stays
// single-purpose and the caller (the CV-parsing consumer) owns the format dispatch, next to the
// same magic-byte checks it already performs.
public interface IDocxTextExtractor
{
    // Synchronous for the same reason as IPdfTextExtractor: CPU-bound, in-memory work on bytes
    // the caller already holds. Returns an empty string when the archive carries no readable
    // document part — the caller treats that the same as a text-less PDF.
    string Extract(byte[] docxBytes);
}
