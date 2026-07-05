using Ats.Modules.Applications.Application;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Ats.Modules.Applications.Infrastructure;

// Consumes CvParseRequestedIntegrationEvent off RabbitMQ and produces the structured CV data
// (Sprint 6.3). This is the out-of-process half of the apply flow: the candidate's request already
// returned 201, and parsing happens afterwards, decoupled.
//
// Steps: download the CV from object storage -> pick the extractor for its real format (PDF or
// DOCX, by magic bytes) -> extract text -> ask the LLM to extract structured fields -> store the
// result in MongoDB. If parsing throws (e.g. a transient LLM failure that outlasts the parser's own
// Polly retries), the exception propagates so MassTransit retries the message and, once retries are
// exhausted, dead-letters it.
//
// No idempotency guard is needed: the store upserts on the application id, so a duplicate delivery
// simply overwrites the same result. (The only cost of a redelivery is a repeat LLM call, which is
// rare and acceptable for the MVP.)
public sealed class CvParsingConsumer : IConsumer<CvParseRequestedIntegrationEvent>
{
    // %PDF and PK\x03\x04 (ZIP, which is what a DOCX package is on disk). We check the real
    // content, not the file name, for the same reason the upload boundary does
    // (FileSignatureValidator): names lie.
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

        // A format outside the upload whitelist (which should not happen while the boundary and
        // this dispatch agree) is acknowledged and skipped rather than retried/dead-lettered:
        // redelivery cannot turn an unknown format into a known one.
        var text = ExtractText(bytes);
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

        // A job that was deleted between application and parsing is an edge case with no requirements
        // left to judge against; the parser still runs, just without job-specific fit (empty
        // description reads as "no requirements stated" to the prompt, same as a job with no
        // description written at all).
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

    // Null means "format we don't extract from"; empty string means "known format, no text".
    // The caller logs the two cases differently, so the distinction is deliberate.
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
