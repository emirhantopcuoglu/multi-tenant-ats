using System.Net;
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
public sealed class InterviewRescheduledEmailConsumer
    : IConsumer<InterviewRescheduledIntegrationEvent>
{
    private const string Subject = "Your interview has been moved";

    private readonly IEmailSender _emailSender;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly InterviewRoomOptions _roomOptions;
    private readonly ILogger<InterviewRescheduledEmailConsumer> _logger;

    public InterviewRescheduledEmailConsumer(
        IEmailSender emailSender,
        IIdempotencyGuard idempotencyGuard,
        IOptions<InterviewRoomOptions> roomOptions,
        ILogger<InterviewRescheduledEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _idempotencyGuard = idempotencyGuard;
        _roomOptions = roomOptions.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewRescheduledIntegrationEvent> context)
    {
        var message = context.Message;

        var firstName = WebUtility.HtmlEncode(message.CandidateFirstName);
        var jobTitle = WebUtility.HtmlEncode(message.JobTitle);
        var interviewType = InterviewEmailFormatting.HumanizeType(message.InterviewType);
        var previous = InterviewEmailFormatting.FormatUtc(message.PreviousScheduledAtUtc);
        var updated = InterviewEmailFormatting.FormatUtc(message.ScheduledAtUtc);

        // The room link survives a reschedule (the token is stable across moves), so the candidate
        // can keep using the same URL — worth saying, because a moved meeting invites the assumption
        // that the old link is dead.
        var joinLine = message.RoomToken is { } roomToken
            ? InterviewEmailFormatting.JoinLine(_roomOptions.BaseUrl, roomToken, unchanged: true)
            : "<p>This is a phone interview — the interviewer will call you at the new time.</p>";

        var body = $"""
            <p>Hi {firstName},</p>
            <p>Your <strong>{interviewType}</strong> interview for <strong>{jobTitle}</strong> has been moved.</p>
            <p>Previously: <s>{previous}</s><br/>
            Now: <strong>{updated}</strong><br/>
            Duration: {message.DurationMinutes} minutes</p>
            {joinLine}
            <p>Please update your calendar. We look forward to speaking with you.</p>
            """;

        var key = $"notifications:interview-rescheduled-email:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key,
            () => _emailSender.SendAsync(message.CandidateEmail, Subject, body, context.CancellationToken));

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
}
