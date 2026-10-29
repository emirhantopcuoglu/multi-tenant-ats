using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Notifications.Infrastructure;

// Emails the candidate when an interview moves, with both the old and new time.
// Idempotent on the message id.
public sealed class InterviewRescheduledEmailConsumer
    : IConsumer<InterviewRescheduledIntegrationEvent>
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ICandidateAccountReader _candidateAccounts;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly InterviewRoomOptions _roomOptions;
    private readonly ILogger<InterviewRescheduledEmailConsumer> _logger;

    public InterviewRescheduledEmailConsumer(
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ICandidateAccountReader candidateAccounts,
        IIdempotencyGuard idempotencyGuard,
        IOptions<InterviewRoomOptions> roomOptions,
        ILogger<InterviewRescheduledEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _emailText = emailText;
        _candidateAccounts = candidateAccounts;
        _idempotencyGuard = idempotencyGuard;
        _roomOptions = roomOptions.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewRescheduledIntegrationEvent> context)
    {
        var message = context.Message;

        var key = $"notifications:interview-rescheduled-email:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key, () => SendAsync(message, context.CancellationToken));

        if (!sent)
        {
            _logger.LogInformation(
                "Skipped duplicate interview-rescheduled email for interview {InterviewId}",
                message.InterviewId);
            return;
        }

        _logger.LogInformation(
            "Sent interview-rescheduled email to {CandidateEmail} for interview {InterviewId}",
            message.CandidateEmail,
            message.InterviewId);
    }

    private async Task SendAsync(InterviewRescheduledIntegrationEvent message, CancellationToken ct)
    {
        var language = await _candidateAccounts.GetPreferredLanguageByEmailAsync(message.CandidateEmail, ct);

        var interviewType = InterviewEmailFormatting.TypeName(message.InterviewType, _emailText, language);
        var previous = InterviewEmailFormatting.FormatUtc(message.PreviousScheduledAtUtc, _emailText, language);
        var updated = InterviewEmailFormatting.FormatUtc(message.ScheduledAtUtc, _emailText, language);

        // The room link doesn't change on reschedule, and the email says so.
        var joinLine = message.RoomToken is { } roomToken
            ? InterviewEmailFormatting.JoinLine(
                _roomOptions.BaseUrl, roomToken, EmailTextKeys.Interview.JoinLineUnchanged,
                _emailText, language)
            : _emailText.Get(EmailTextKeys.Interview.PhoneLineRescheduled, language);

        var body = _emailText.Get(
            EmailTextKeys.Interview.RescheduledBody,
            language,
            WebUtility.HtmlEncode(message.CandidateFirstName),
            interviewType,
            WebUtility.HtmlEncode(message.JobTitle),
            previous,
            updated,
            message.DurationMinutes,
            joinLine);

        await _emailSender.SendAsync(
            message.CandidateEmail,
            _emailText.Get(EmailTextKeys.Interview.RescheduledSubject, language),
            body,
            ct);
    }
}
