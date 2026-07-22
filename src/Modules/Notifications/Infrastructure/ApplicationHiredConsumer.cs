using System.Net;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Consumes ApplicationHiredIntegrationEvent off RabbitMQ and emails the candidate the good news —
// the positive counterpart of ApplicationRejectedConsumer, with the same retry/dead-letter policy
// and the same idempotency guard keyed on the message id, so an at-least-once redelivery does not
// email the candidate twice.
public sealed class ApplicationHiredConsumer : IConsumer<ApplicationHiredIntegrationEvent>
{
    private const string Subject = "Congratulations — you got the job!";

    // Fallback when the job title is unavailable (e.g. the role was deleted) so the sentence still
    // reads naturally without leaking that anything is missing.
    private const string FallbackRole = "the role you applied for";

    private readonly IEmailSender _emailSender;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationHiredConsumer> _logger;

    public ApplicationHiredConsumer(
        IEmailSender emailSender,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationHiredConsumer> logger)
    {
        _emailSender = emailSender;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationHiredIntegrationEvent> context)
    {
        var message = context.Message;

        // The candidate's name comes from the public apply form and the job title from a recruiter;
        // both are untrusted in an HTML email, so HTML-encode them to prevent content injection.
        var firstName = WebUtility.HtmlEncode(message.CandidateFirstName);
        var role = string.IsNullOrWhiteSpace(message.JobTitle)
            ? FallbackRole
            : $"<strong>{WebUtility.HtmlEncode(message.JobTitle)}</strong>";

        var body = $"""
            <p>Hi {firstName},</p>
            <p>Great news — the company has decided to hire you for {role}. Congratulations!</p>
            <p>They will contact you directly with the next steps.</p>
            """;

        var key = $"notifications:application-hired:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key,
            () => _emailSender.SendAsync(message.CandidateEmail, Subject, body, context.CancellationToken));

        if (!sent)
        {
            _logger.LogInformation(
                "Skipped duplicate application-hired email for application {ApplicationId}",
                message.ApplicationId);
            return;
        }

        _logger.LogInformation(
            "Sent application-hired email to {CandidateEmail} for application {ApplicationId}",
            message.CandidateEmail,
            message.ApplicationId);
    }
}
