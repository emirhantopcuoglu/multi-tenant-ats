using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Notifications.Infrastructure;

// Consumes InterviewScheduledIntegrationEvent off RabbitMQ and emails the candidate their
// interview details — the email counterpart to InterviewScheduledNotificationConsumer's in-app row
// (roadmap 3.4). Own queue, so a slow or failing SMTP send never blocks the bell from updating.
//
// The send is wrapped in the idempotency guard keyed on the message id, matching the other email
// consumers: an at-least-once redelivery must not email the candidate twice.
//
// Wording comes from IEmailTextProvider in the language the candidate's account carries; see
// ApplicationRejectedConsumer for why the language is read here rather than carried on the event.
public sealed class InterviewScheduledEmailConsumer
    : IConsumer<InterviewScheduledIntegrationEvent>
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ICandidateAccountReader _candidateAccounts;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly InterviewRoomOptions _roomOptions;
    private readonly ILogger<InterviewScheduledEmailConsumer> _logger;

    public InterviewScheduledEmailConsumer(
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ICandidateAccountReader candidateAccounts,
        IIdempotencyGuard idempotencyGuard,
        IOptions<InterviewRoomOptions> roomOptions,
        ILogger<InterviewScheduledEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _emailText = emailText;
        _candidateAccounts = candidateAccounts;
        _idempotencyGuard = idempotencyGuard;
        _roomOptions = roomOptions.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewScheduledIntegrationEvent> context)
    {
        var message = context.Message;

        var key = $"notifications:interview-scheduled-email:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key, () => SendAsync(message, context.CancellationToken));

        if (!sent)
        {
            _logger.LogInformation(
                "Skipped duplicate interview-scheduled email for interview {InterviewId}",
                message.InterviewId);
            return;
        }

        _logger.LogInformation(
            "Sent interview-scheduled email to {CandidateEmail} for interview {InterviewId}",
            message.CandidateEmail,
            message.InterviewId);
    }

    private async Task SendAsync(InterviewScheduledIntegrationEvent message, CancellationToken ct)
    {
        var language = await _candidateAccounts.GetPreferredLanguageByEmailAsync(message.CandidateEmail, ct);

        // Job title is a recruiter/company-controlled string, untrusted in an HTML email, so
        // HTML-encode it — same rule as the other emails. The date format and the type name are
        // shared with the reschedule/cancel emails so one interview never reads differently across
        // the three messages.
        var interviewType = InterviewEmailFormatting.TypeName(message.InterviewType, _emailText, language);
        var scheduledAt = InterviewEmailFormatting.FormatUtc(message.ScheduledAtUtc, _emailText, language);

        // A phone screen has no room token, so there is no link to send — the candidate is called
        // instead. Every other type gets a join link.
        var joinLine = message.RoomToken is { } roomToken
            ? InterviewEmailFormatting.JoinLine(
                _roomOptions.BaseUrl, roomToken, EmailTextKeys.Interview.JoinLine,
                _emailText, language)
            : _emailText.Get(EmailTextKeys.Interview.PhoneLineScheduled, language);

        var body = _emailText.Get(
            EmailTextKeys.Interview.ScheduledBody,
            language,
            WebUtility.HtmlEncode(message.CandidateFirstName),
            WebUtility.HtmlEncode(message.JobTitle),
            interviewType,
            scheduledAt,
            message.DurationMinutes,
            joinLine);

        await _emailSender.SendAsync(
            message.CandidateEmail,
            _emailText.Get(EmailTextKeys.Interview.ScheduledSubject, language),
            body,
            ct);
    }
}
