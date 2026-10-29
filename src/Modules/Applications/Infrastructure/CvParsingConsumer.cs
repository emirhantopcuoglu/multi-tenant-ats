using Ats.Modules.Applications.Application;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Ats.Modules.Applications.Infrastructure;

// Downloads the CV, extracts text (PDF or DOCX, by magic bytes), asks the LLM for structured data
// and stores it in MongoDB. Failures propagate so MassTransit retries. No idempotency guard: the
// store upserts on the application id.
public sealed class CvParsingConsumer : IConsumer<CvParseRequestedIntegrationEvent>
{
    // Detected from the content, not the file name.
    private static readonly byte[] PdfMagic = [0x25, 0x50, 0x44, 0x46];
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    private readonly IFileStorage _fileStorage;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IDocxTextExtractor _docxTextExtractor;
    private readonly ICvParser _cvParser;
    private readonly ICvParseResultRepository _repository;
    private readonly IJobDirectory _jobDirectory;
    private readonly ILogger<CvParsingConsumer> _logger;

    public CvParsingConsumer(
        IFileStorage fileStorage,
        IPdfTextExtractor pdfTextExtractor,
        IDocxTextExtractor docxTextExtractor,
        ICvParser cvParser,
        ICvParseResultRepository repository,
        IJobDirectory jobDirectory,
        ILogger<CvParsingConsumer> logger)
    {
        _fileStorage = fileStorage;
        _pdfTextExtractor = pdfTextExtractor;
        _docxTextExtractor = docxTextExtractor;
        _cvParser = cvParser;
        _repository = repository;
        _jobDirectory = jobDirectory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CvParseRequestedIntegrationEvent> context)
    {
        var message = context.Message;
        var bytes = await _fileStorage.DownloadAsync(message.CvFileKey, context.CancellationToken);

        // An unknown format is skipped, not retried; a retry can't change it.
        string? text;
        try
        {
            text = ExtractText(bytes);
        }
        catch (TextExtractionException exception)
        {
            // Same for a corrupted file: it would fail the same way on every retry.
            _logger.LogWarning(
                exception,
                "Skipping CV parse for application {ApplicationId}: the file could not be read.",
                message.ApplicationId);
            return;
        }

        if (text is null)
        {
            _logger.LogWarning(
                "Skipping CV parse for application {ApplicationId}: file is neither PDF nor DOCX.",
                message.ApplicationId);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning(
                "Skipping CV parse for application {ApplicationId}: the file has no extractable text.",
                message.ApplicationId);
            return;
        }

        // A deleted job just means no requirements to compare against.
        var job = await _jobDirectory.GetJobRequirementsAsync(
            message.TenantId, message.JobId, context.CancellationToken);

        CvParseResult result;
        using (AppMetrics.CvParsingDurationSeconds.NewTimer())
        {
            result = await _cvParser.ParseAsync(text, job?.Description ?? "", context.CancellationToken);
        }

        await _repository.SaveAsync(
            message.TenantId, message.ApplicationId, result, DateTime.UtcNow, context.CancellationToken);

        _logger.LogInformation(
            "Stored CV parse result for application {ApplicationId} ({SkillCount} skills).",
            message.ApplicationId, result.Skills.Count);
    }

    // Null means unsupported format; empty means no text. Logged differently.
    private string? ExtractText(byte[] bytes)
    {
        if (StartsWith(bytes, PdfMagic))
            return _pdfTextExtractor.Extract(bytes);

        if (StartsWith(bytes, ZipMagic))
            return _docxTextExtractor.Extract(bytes);

        return null;
    }

    private static bool StartsWith(byte[] bytes, byte[] magic) =>
        bytes.Length >= magic.Length && bytes.AsSpan(0, magic.Length).SequenceEqual(magic);
}
