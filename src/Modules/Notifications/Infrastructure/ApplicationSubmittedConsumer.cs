using System.Net;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Consumes ApplicationSubmittedIntegrationEvent off RabbitMQ and emails the candidate a
// confirmation. This is the out-of-process side of the apply flow: the candidate's HTTP request has
// already returned 201, and this work happens afterwards, decoupled. If it throws, MassTransit
// redelivers the message, so a transient SMTP failure does not silently drop the email.
//
// The body is built with interpolated HTML, matching the existing invitation email — a templating
// engine is deliberately deferred until the set of emails justifies one (see the architecture guide).
public sealed class ApplicationSubmittedConsumer : IConsumer<ApplicationSubmittedIntegrationEvent>
{
    private const string Subject = "We received your application";

    private readonly IEmailSender _emailSender;
    private readonly ILogger<ApplicationSubmittedConsumer> _logger;

    public ApplicationSubmittedConsumer(
        IEmailSender emailSender,
        ILogger<ApplicationSubmittedConsumer> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationSubmittedIntegrationEvent> context)
    {
        var message = context.Message;

        // The candidate's name comes from the public apply form and the job title from a recruiter;
        // both are untrusted in an HTML email, so HTML-encode them to prevent content injection.
        var firstName = WebUtility.HtmlEncode(message.CandidateFirstName);
        var jobTitle = WebUtility.HtmlEncode(message.JobTitle);

        var body = $"""
            <p>Hi {firstName},</p>
            <p>Thanks for applying to <strong>{jobTitle}</strong>. We have received your application
            and our team will review it shortly.</p>
            <p>We'll be in touch.</p>
            """;

        await _emailSender.SendAsync(message.CandidateEmail, Subject, body, context.CancellationToken);

        _logger.LogInformation(
            "Sent application-received email to {CandidateEmail} for application {ApplicationId}",
            message.CandidateEmail,
            message.ApplicationId);
    }
}
