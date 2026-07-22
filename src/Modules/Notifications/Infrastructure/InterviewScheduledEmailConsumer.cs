using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Consumes InterviewScheduledIntegrationEvent off RabbitMQ and emails the candidate their
// interview details — the email counterpart to InterviewScheduledNotificationConsumer's in-app row
// (roadmap 3.4). Own queue, so a slow or failing SMTP send never blocks the bell from updating.
//
// The send is wrapped in the idempotency guard keyed on the message id, matching the other email
// consumers: an at-least-once redelivery must not email the candidate twice.
public sealed partial class InterviewScheduledEmailConsumer
    : IConsumer<InterviewScheduledIntegrationEvent>
{
    private const string Subject = "Your interview has been scheduled";

    private readonly IEmailSender _emailSender;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<InterviewScheduledEmailConsumer> _logger;

    public InterviewScheduledEmailConsumer(
        IEmailSender emailSender,
        IIdempotencyGuard idempotencyGuard,
        ILogger<InterviewScheduledEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewScheduledIntegrationEvent> context)
    {
        var message = context.Message;

        // Job title and location are recruiter/company-controlled strings, untrusted in an HTML
        // email, so HTML-encode them — same rule as the other emails. The interview type is a
        // closed set of PascalCase contract values (e.g. "PhoneScreen"); humanize it for reading.
        var firstName = WebUtility.HtmlEncode(message.CandidateFirstName);
        var jobTitle = WebUtility.HtmlEncode(message.JobTitle);
        var interviewType = HumanizePascalCase(message.InterviewType);
        var scheduledAt = message.ScheduledAtUtc.ToString(
            "dddd, MMMM d, yyyy 'at' h:mm tt 'UTC'", CultureInfo.InvariantCulture);
        var locationLine = string.IsNullOrWhiteSpace(message.Location)
            ? string.Empty
            : $"<p>Location: {WebUtility.HtmlEncode(message.Location)}</p>";

        var body = $"""
            <p>Hi {firstName},</p>
            <p>An interview has been scheduled for your application to <strong>{jobTitle}</strong>.</p>
            <p>Type: {interviewType}<br/>
            When: {scheduledAt}<br/>
            Duration: {message.DurationMinutes} minutes</p>
            {locationLine}
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

    // "PhoneScreen" -> "Phone Screen". The pattern is a zero-width lookaround (matches the boundary,
    // consumes nothing), so the replacement is a plain space, not a backreference.
    private static string HumanizePascalCase(string value) =>
        PascalCaseBoundary().Replace(value, " ");

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
