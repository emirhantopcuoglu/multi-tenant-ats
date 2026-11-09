using System.Diagnostics;
using System.Text;
using Ats.PdfWorker;
using Ats.Shared.Kernel;

namespace Ats.Shared.Infrastructure;

// A PDF of a few kilobytes can make PdfPig use gigabytes, and PdfPig can't be stopped halfway, so each
// PDF is read by Ats.PdfWorker in its own process with a memory and a time limit.
public sealed class PdfWorkerTextExtractor : IPdfTextExtractor
{
    public const int MemoryLimitMegabytes = 256;

    private static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromSeconds(20);
    private static readonly string WorkerPath = Path.Combine(AppContext.BaseDirectory, "Ats.PdfWorker.dll");

    private readonly TimeSpan _timeLimit;

    public PdfWorkerTextExtractor(TimeSpan? timeLimit = null) => _timeLimit = timeLimit ?? DefaultTimeLimit;

    public async Task<string> ExtractAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        using var worker = Process.Start(WorkerStartInfo())
            ?? throw new InvalidOperationException("The PDF worker did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeLimit);

        try
        {
            var output = worker.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = worker.StandardError.ReadToEndAsync(timeout.Token);
            await worker.StandardInput.BaseStream.WriteAsync(pdfBytes, timeout.Token);
            worker.StandardInput.Close();
            await worker.WaitForExitAsync(timeout.Token);

            return worker.ExitCode switch
            {
                WorkerExitCode.Success => await output,
                WorkerExitCode.Malformed => throw new TextExtractionException($"The PDF is malformed: {await error}"),
                WorkerExitCode.OutOfMemory => throw new TextExtractionException(
                    $"Reading the PDF needed more than {MemoryLimitMegabytes} MB."),
                var code => throw new InvalidOperationException($"The PDF worker exited with code {code}: {await error}"),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TextExtractionException($"Reading the PDF took longer than {_timeLimit.TotalSeconds} s.");
        }
        finally
        {
            if (!worker.HasExited)
                worker.Kill(entireProcessTree: true);
        }
    }

    private static ProcessStartInfo WorkerStartInfo()
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(WorkerPath);
        // The runtime reads this setting as hex.
        startInfo.Environment["DOTNET_GCHeapHardLimit"] = (MemoryLimitMegabytes * 1024L * 1024).ToString("X");
        return startInfo;
    }
}
