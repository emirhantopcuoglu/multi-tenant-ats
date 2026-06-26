namespace Ats.Shared.Kernel;

// Port over "turn a PDF's bytes into plain text". Deterministic and library-backed (PdfPig in
// Infrastructure), so it stays out of the LLM port: ICvParser receives text, never a file. Split
// out as its own behaviour so the CV-parsing consumer depends on extraction without taking a
// direct dependency on the PDF library.
public interface IPdfTextExtractor
{
    // Synchronous because the underlying extraction is CPU-bound, in-memory work with no I/O —
    // wrapping it in a Task would only add overhead. The caller already has the bytes in hand.
    string Extract(byte[] pdfBytes);
}
