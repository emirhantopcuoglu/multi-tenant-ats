using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Notifications.Infrastructure;

// Keyed on (interview, kind), not the message id: the sweep can republish the same reminder
// under a new message id after a failed save.
public sealed class InterviewReminderEmailConsumer
    : IConsumer<InterviewReminderDueIntegrationEvent>
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ICandidateAccountReader _candidateAccounts;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly InterviewRoomOptions _roomOptions;
    private readonly ILogger<InterviewReminderEmailConsumer> _logger;

    public InterviewReminderEmailConsumer(
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ICandidateAccountReader candidateAccounts,
        IIdempotencyGuard idempotencyGuard,
        IOptions<InterviewRoomOptions> roomOptions,
        ILogger<InterviewReminderEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _emailText = emailText;
        _candidateAccounts = candidateAccounts;
        _idempotencyGuard = idempotencyGuard;
        _roomOptions = roomOptions.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewReminderDueIntegrationEvent> context)
    {
        var message = context.Message;

        var key = $"notifications:interview-reminder-email:{message.InterviewId}:{message.Kind}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key, () => SendAsync(message, context.CancellationToken));

        if (!sent)
        {
            _logger.LogInformation(
                "Skipped duplicate {ReminderKind} reminder email for interview {InterviewId}",
                message.Kind,
                message.InterviewId);
            return;
        }

        _logger.LogInformation(
            "Sent {ReminderKind} reminder email to {CandidateEmail} for interview {InterviewId}",
            message.Kind,
            message.CandidateEmail,
            message.InterviewId);
    }

    private async Task SendAsync(InterviewReminderDueIntegrationEvent message, CancellationToken ct)
    {
        var language = await _candidateAccounts.GetPreferredLanguageByEmailAsync(message.CandidateEmail, ct);
        var isStartingSoon = message.Kind == InterviewReminderKinds.StartingSoon;

        var interviewType = InterviewEmailFormatting.TypeName(message.InterviewType, _emailText, language);
        var scheduledAt = InterviewEmailFormatting.FormatUtc(message.ScheduledAtUtc, _emailText, language);

        // Starting-soon goes out when the room opens, so it says "join now". Phone screens have no room.
        var joinLine = message.RoomToken is { } roomToken
            ? InterviewEmailFormatting.JoinLine(
                _roomOptions.BaseUrl,
                roomToken,
                isStartingSoon ? EmailTextKeys.Interview.JoinLineNow : EmailTextKeys.Interview.JoinLine,
                _emailText,
                language)
            : _emailText.Get(
                isStartingSoon
                    ? EmailTextKeys.Interview.PhoneLineStartingSoon
                    : EmailTextKeys.Interview.PhoneLineScheduled,
                language);

        var body = _emailText.Get(
            isStartingSoon
                ? EmailTextKeys.Interview.ReminderStartingSoonBody
                : EmailTextKeys.Interview.ReminderDayBeforeBody,
            language,
            WebUtility.HtmlEncode(message.CandidateFirstName),
            interviewType,
            WebUtility.HtmlEncode(message.JobTitle),
            scheduledAt,
            message.DurationMinutes,
            joinLine);

        var subject = _emailText.Get(
            isStartingSoon
                ? EmailTextKeys.Interview.ReminderStartingSoonSubject
                : EmailTextKeys.Interview.ReminderDayBeforeSubject,
            language);

        await _emailSender.SendAsync(message.CandidateEmail, subject, body, ct);
    }
}
