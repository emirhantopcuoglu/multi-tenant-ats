using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Notifications.Infrastructure;

// Emails the candidate when their interview moves. Own queue, so a slow SMTP send never blocks the
// in-app feed; idempotency guard on the message id so a redelivery cannot email twice.
//
// The email leads with the old time as well as the new one. A candidate who has the original
// invitation in their inbox and the original slot in their calendar needs to see what changed, not
// just be handed a second set of details to reconcile.
//
// Wording comes from IEmailTextProvider in the language the candidate's account carries; see
// ApplicationRejectedConsumer for why the language is read here rather than carried on the event.
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

        // The room link survives a reschedule (the token is stable across moves), so the candidate
        // can keep using the same URL — worth saying, because a moved meeting invites the assumption
        // that the old link is dead.
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
