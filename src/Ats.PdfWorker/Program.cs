// Started by PdfWorkerTextExtractor: PDF bytes on stdin, text on stdout, the result in the exit code.

using System.Text;
using Ats.PdfWorker;
using UglyToad.PdfPig.Core;

using var input = new MemoryStream();
Console.OpenStandardInput().CopyTo(input);

try
{
    var text = PdfText.Extract(input.ToArray());
    using var output = Console.OpenStandardOutput();
    output.Write(Encoding.UTF8.GetBytes(text));
    return WorkerExitCode.Success;
}
catch (PdfDocumentFormatException exception)
{
    Console.Error.Write(exception.Message);
    return WorkerExitCode.Malformed;
}
catch (OutOfMemoryException)
{
    return WorkerExitCode.OutOfMemory;
}
