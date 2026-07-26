using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Consumes ApplicationSubmittedIntegrationEvent off RabbitMQ and emails the candidate a
// confirmation. This is the out-of-process side of the apply flow: the candidate's HTTP request has
// already returned 201, and this work happens afterwards, decoupled. If it throws, MassTransit
// retries the message and, once retries are exhausted, dead-letters it — so a transient SMTP failure
// does not silently drop the email.
//
// The send is wrapped in the idempotency guard keyed on the message id, so an at-least-once
// redelivery of the same message does not email the candidate twice (Sprint 5.5).
//
// Wording comes from IEmailTextProvider in the language the candidate account carries, which is why
// this consumer reads ICandidateAccountReader: the event identifies the recipient by address, and the
// language belongs to the account behind it. That read happens inside the idempotency guard so a
// duplicate delivery costs nothing.
public sealed class ApplicationSubmittedConsumer : IConsumer<ApplicationSubmittedIntegrationEvent>
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ICandidateAccountReader _candidateAccounts;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationSubmittedConsumer> _logger;

    public ApplicationSubmittedConsumer(
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ICandidateAccountReader candidateAccounts,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationSubmittedConsumer> logger)
    {
        _emailSender = emailSender;
        _emailText = emailText;
        _candidateAccounts = candidateAccounts;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationSubmittedIntegrationEvent> context)
    {
        var message = context.Message;

        var key = $"notifications:application-submitted:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(key, () => SendAsync(message, context.CancellationToken));

        if (!sent)
        {
            _logger.LogInformation(
                "Skipped duplicate application-received email for application {ApplicationId}",
                message.ApplicationId);
            return;
        }

        AppMetrics.ApplicationsSubmittedTotal.Inc();
        _logger.LogInformation(
            "Sent application-received email to {CandidateEmail} for application {ApplicationId}",
            message.CandidateEmail,
            message.ApplicationId);
    }

    private async Task SendAsync(ApplicationSubmittedIntegrationEvent message, CancellationToken ct)
    {
        var language = await _candidateAccounts.GetPreferredLanguageByEmailAsync(message.CandidateEmail, ct);

        // The candidate's name comes from the public apply form and the job title from a recruiter;
        // both are untrusted in an HTML email, so HTML-encode them to prevent content injection.
        var body = _emailText.Get(
            EmailTextKeys.Application.SubmittedBody,
            language,
            WebUtility.HtmlEncode(message.CandidateFirstName),
            WebUtility.HtmlEncode(message.JobTitle));

        await _emailSender.SendAsync(
            message.CandidateEmail,
            _emailText.Get(EmailTextKeys.Application.SubmittedSubject, language),
            body,
            ct);
    }
}
