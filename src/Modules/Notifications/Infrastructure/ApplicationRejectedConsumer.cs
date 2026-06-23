using System.Net;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Consumes ApplicationRejectedIntegrationEvent off RabbitMQ and emails the candidate a polite
// rejection. The out-of-process counterpart to the recruiter's reject action, which has already
// returned 204. If it throws, MassTransit retries the message and, once retries are exhausted,
// dead-letters it — so a transient SMTP failure does not silently drop the email.
//
// The send is wrapped in the idempotency guard keyed on the message id, so an at-least-once
// redelivery of the same message does not email the candidate twice (Sprint 5.5).
//
// The email is deliberately generic: it never includes the recruiter's internal rejection reason.
// The body is interpolated HTML, matching the existing emails — a templating engine is deferred
// until the set of emails justifies one.
public sealed class ApplicationRejectedConsumer : IConsumer<ApplicationRejectedIntegrationEvent>
{
    private const string Subject = "Update on your application";

    // Fallback when the job title is unavailable (e.g. the role was deleted) so the sentence still
    // reads naturally without leaking that anything is missing.
    private const string FallbackRole = "the role you applied for";

    private readonly IEmailSender _emailSender;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationRejectedConsumer> _logger;

    public ApplicationRejectedConsumer(
        IEmailSender emailSender,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationRejectedConsumer> logger)
    {
        _emailSender = emailSender;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationRejectedIntegrationEvent> context)
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
            <p>Thank you for your interest in {role} and for the time you put into your application.</p>
            <p>After careful consideration, we have decided not to move forward with your application
            at this time.</p>
            <p>We appreciate your interest and encourage you to apply for future openings that match
            your experience. We wish you all the best.</p>
            """;

        var key = $"notifications:application-rejected:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key,
            () => _emailSender.SendAsync(message.CandidateEmail, Subject, body, context.CancellationToken));

        if (!sent)
        {
            _logger.LogInformation(
                "Skipped duplicate application-rejected email for application {ApplicationId}",
                message.ApplicationId);
            return;
        }

        _logger.LogInformation(
            "Sent application-rejected email to {CandidateEmail} for application {ApplicationId}",
            message.CandidateEmail,
            message.ApplicationId);
    }
}
