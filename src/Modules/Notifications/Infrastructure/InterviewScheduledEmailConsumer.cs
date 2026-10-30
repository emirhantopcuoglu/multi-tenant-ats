using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Notifications.Infrastructure;

// Emails the candidate the interview details. Idempotent on the message id.
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

        // Job title comes from the company, so it is encoded.
        var interviewType = InterviewEmailFormatting.TypeName(message.InterviewType, _emailText, language);
        var scheduledAt = InterviewEmailFormatting.FormatUtc(message.ScheduledAtUtc, _emailText, language);

        // Phone screens have no room link.
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
