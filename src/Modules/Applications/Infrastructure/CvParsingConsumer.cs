using Ats.Modules.Applications.Application;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Applications.Infrastructure;

// Consumes CvParseRequestedIntegrationEvent off RabbitMQ and produces the structured CV data
// (Sprint 6.3). This is the out-of-process half of the apply flow: the candidate's request already
// returned 201, and parsing happens afterwards, decoupled.
//
// Steps: download the CV from object storage -> confirm it is a PDF -> extract text -> ask Claude to
// extract structured fields -> store the result in MongoDB. If parsing throws (e.g. a transient LLM
// failure that outlasts the parser's own Polly retries), the exception propagates so MassTransit
// retries the message and, once retries are exhausted, dead-letters it.
//
// No idempotency guard is needed: the store upserts on the application id, so a duplicate delivery
// simply overwrites the same result. (The only cost of a redelivery is a repeat LLM call, which is
// rare and acceptable for the MVP.)
public sealed class CvParsingConsumer : IConsumer<CvParseRequestedIntegrationEvent>
{
    // %PDF — the leading bytes of every PDF. We check the real content, not the file name, for the
    // same reason the upload boundary does (FileSignatureValidator): names lie.
    private static readonly byte[] PdfMagic = [0x25, 0x50, 0x44, 0x46];

    private readonly IFileStorage _fileStorage;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly ICvParser _cvParser;
    private readonly ICvParseResultRepository _repository;
    private readonly ILogger<CvParsingConsumer> _logger;

    public CvParsingConsumer(
        IFileStorage fileStorage,
        IPdfTextExtractor pdfTextExtractor,
        ICvParser cvParser,
        ICvParseResultRepository repository,
        ILogger<CvParsingConsumer> logger)
    {
        _fileStorage = fileStorage;
        _pdfTextExtractor = pdfTextExtractor;
        _cvParser = cvParser;
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CvParseRequestedIntegrationEvent> context)
    {
        var message = context.Message;
        var bytes = await _fileStorage.DownloadAsync(message.CvFileKey, context.CancellationToken);

        // MVP text extraction is PDF-only (PdfPig). A non-PDF CV (e.g. DOCX) is a valid upload but
        // not something we can parse yet, so we acknowledge and skip rather than retry/dead-letter
        // it — a DOCX extractor is a later increment.
        if (!IsPdf(bytes))
        {
            _logger.LogWarning(
                "Skipping CV parse for application {ApplicationId}: file is not a PDF.",
                message.ApplicationId);
            return;
        }

        var text = _pdfTextExtractor.Extract(bytes);
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning(
                "Skipping CV parse for application {ApplicationId}: the PDF has no extractable text.",
                message.ApplicationId);
            return;
        }

        var result = await _cvParser.ParseAsync(text, context.CancellationToken);

        await _repository.SaveAsync(
            message.TenantId, message.ApplicationId, result, DateTime.UtcNow, context.CancellationToken);

        _logger.LogInformation(
            "Stored CV parse result for application {ApplicationId} ({SkillCount} skills).",
            message.ApplicationId, result.Skills.Count);
    }

    private static bool IsPdf(byte[] bytes) =>
        bytes.Length >= PdfMagic.Length && bytes.AsSpan(0, PdfMagic.Length).SequenceEqual(PdfMagic);
}
