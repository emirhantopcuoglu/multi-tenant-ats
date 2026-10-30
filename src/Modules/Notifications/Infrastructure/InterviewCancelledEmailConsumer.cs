using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Emails the candidate when an interview is cancelled. The reason decides whether the email says
// another invitation is coming. The recruiter's note isn't on the event.
public sealed class InterviewCancelledEmailConsumer
    : IConsumer<InterviewCancelledIntegrationEvent>
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ICandidateAccountReader _candidateAccounts;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<InterviewCancelledEmailConsumer> _logger;

    public InterviewCancelledEmailConsumer(
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ICandidateAccountReader candidateAccounts,
        IIdempotencyGuard idempotencyGuard,
        ILogger<InterviewCancelledEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _emailText = emailText;
        _candidateAccounts = candidateAccounts;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewCancelledIntegrationEvent> context)
    {
        var message = context.Message;

        var key = $"notifications:interview-cancelled-email:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key, () => SendAsync(message, context.CancellationToken));

        if (!sent)
        {
            _logger.LogInformation(
                "Skipped duplicate interview-cancelled email for interview {InterviewId}",
                message.InterviewId);
            return;
        }

        _logger.LogInformation(
            "Sent interview-cancelled email to {CandidateEmail} for interview {InterviewId}",
            message.CandidateEmail,
            message.InterviewId);
    }

    private async Task SendAsync(InterviewCancelledIntegrationEvent message, CancellationToken ct)
    {
        var language = await _candidateAccounts.GetPreferredLanguageByEmailAsync(message.CandidateEmail, ct);

        var interviewType = InterviewEmailFormatting.TypeName(message.InterviewType, _emailText, language);
        var scheduledAt = InterviewEmailFormatting.FormatUtc(message.ScheduledAtUtc, _emailText, language);
        var closing = InterviewEmailFormatting.CancellationClosing(message.Reason, _emailText, language);

        var body = _emailText.Get(
            EmailTextKeys.Interview.CancelledBody,
            language,
            WebUtility.HtmlEncode(message.CandidateFirstName),
            interviewType,
            WebUtility.HtmlEncode(message.JobTitle),
            scheduledAt,
            closing);

        await _emailSender.SendAsync(
            message.CandidateEmail,
            _emailText.Get(EmailTextKeys.Interview.CancelledSubject, language),
            body,
            ct);
    }
}
