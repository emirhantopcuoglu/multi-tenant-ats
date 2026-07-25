using System.Net;
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
public sealed class InterviewScheduledEmailConsumer
    : IConsumer<InterviewScheduledIntegrationEvent>
{
    private const string Subject = "Your interview has been scheduled";

    private readonly IEmailSender _emailSender;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly InterviewRoomOptions _roomOptions;
    private readonly ILogger<InterviewScheduledEmailConsumer> _logger;

    public InterviewScheduledEmailConsumer(
        IEmailSender emailSender,
        IIdempotencyGuard idempotencyGuard,
        IOptions<InterviewRoomOptions> roomOptions,
        ILogger<InterviewScheduledEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _idempotencyGuard = idempotencyGuard;
        _roomOptions = roomOptions.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewScheduledIntegrationEvent> context)
    {
        var message = context.Message;

        // Job title is a recruiter/company-controlled string, untrusted in an HTML email, so
        // HTML-encode it — same rule as the other emails. Date formatting and the PascalCase
        // humanization are shared with the reschedule/cancel emails so one interview never reads
        // differently across the three messages.
        var firstName = WebUtility.HtmlEncode(message.CandidateFirstName);
        var jobTitle = WebUtility.HtmlEncode(message.JobTitle);
        var interviewType = InterviewEmailFormatting.HumanizeType(message.InterviewType);
        var scheduledAt = InterviewEmailFormatting.FormatUtc(message.ScheduledAtUtc);

        // A phone screen has no room token, so there is no link to send — the candidate is called
        // instead. Every other type gets a join link.
        var joinLine = message.RoomToken is { } roomToken
            ? InterviewEmailFormatting.JoinLine(_roomOptions.BaseUrl, roomToken, unchanged: false)
            : "<p>This is a phone interview — the interviewer will call you at the scheduled time.</p>";

        var body = $"""
            <p>Hi {firstName},</p>
            <p>An interview has been scheduled for your application to <strong>{jobTitle}</strong>.</p>
            <p>Type: {interviewType}<br/>
            When: {scheduledAt}<br/>
            Duration: {message.DurationMinutes} minutes</p>
            {joinLine}
            <p>We look forward to speaking with you.</p>
            """;

        var key = $"notifications:interview-scheduled-email:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key,
            () => _emailSender.SendAsync(message.CandidateEmail, Subject, body, context.CancellationToken));

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
}
