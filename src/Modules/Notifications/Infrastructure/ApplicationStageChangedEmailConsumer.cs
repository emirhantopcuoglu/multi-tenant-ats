using System.Net;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Consumes ApplicationStageChangedIntegrationEvent off RabbitMQ and emails the candidate that
// their application moved forward — the email counterpart to
// ApplicationStageChangedNotificationConsumer's in-app row (roadmap 3.4). Own queue, so a slow or
// failing SMTP send never blocks the bell from updating, and vice versa.
//
// The send is wrapped in the idempotency guard keyed on the message id, matching the other email
// consumers: an at-least-once redelivery must not email the candidate twice.
public sealed class ApplicationStageChangedEmailConsumer
    : IConsumer<ApplicationStageChangedIntegrationEvent>
{
    private const string Subject = "Your application has moved forward";

    private readonly IEmailSender _emailSender;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationStageChangedEmailConsumer> _logger;

    public ApplicationStageChangedEmailConsumer(
        IEmailSender emailSender,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationStageChangedEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationStageChangedIntegrationEvent> context)
    {
        var message = context.Message;

        // Job title and stage name are recruiter/company-controlled strings, untrusted in an HTML
        // email, so HTML-encode them to prevent content injection — same rule as the other emails.
        var firstName = WebUtility.HtmlEncode(message.CandidateFirstName);
        var jobTitle = WebUtility.HtmlEncode(message.JobTitle);
        var stageName = WebUtility.HtmlEncode(message.ToStageName);

        var body = $"""
            <p>Hi {firstName},</p>
            <p>Your application for <strong>{jobTitle}</strong> has moved to the
            <strong>{stageName}</strong> stage.</p>
            <p>We'll be in touch with next steps.</p>
            """;

        var key = $"notifications:application-stage-changed-email:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key,
            () => _emailSender.SendAsync(message.CandidateEmail, Subject, body, context.CancellationToken));

        if (!sent)
        {
            _logger.LogInformation(
                "Skipped duplicate stage-changed email for application {ApplicationId}",
                message.ApplicationId);
            return;
        }

        _logger.LogInformation(
            "Sent stage-changed email to {CandidateEmail} for application {ApplicationId}",
            message.CandidateEmail,
            message.ApplicationId);
    }
}
