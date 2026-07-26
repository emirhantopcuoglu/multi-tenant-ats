using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
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
//
// Wording comes from IEmailTextProvider in the language the candidate's account carries; see
// ApplicationRejectedConsumer for why the language is read here rather than carried on the event.
public sealed class ApplicationStageChangedEmailConsumer
    : IConsumer<ApplicationStageChangedIntegrationEvent>
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ICandidateAccountReader _candidateAccounts;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationStageChangedEmailConsumer> _logger;

    public ApplicationStageChangedEmailConsumer(
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ICandidateAccountReader candidateAccounts,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationStageChangedEmailConsumer> logger)
    {
        _emailSender = emailSender;
        _emailText = emailText;
        _candidateAccounts = candidateAccounts;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationStageChangedIntegrationEvent> context)
    {
        var message = context.Message;

        var key = $"notifications:application-stage-changed-email:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key, () => SendAsync(message, context.CancellationToken));

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

    private async Task SendAsync(ApplicationStageChangedIntegrationEvent message, CancellationToken ct)
    {
        var language = await _candidateAccounts.GetPreferredLanguageByEmailAsync(message.CandidateEmail, ct);

        // Job title and stage name are recruiter/company-controlled strings, untrusted in an HTML
        // email, so HTML-encode them to prevent content injection — same rule as the other emails.
        var body = _emailText.Get(
            EmailTextKeys.Application.StageChangedBody,
            language,
            WebUtility.HtmlEncode(message.CandidateFirstName),
            WebUtility.HtmlEncode(message.JobTitle),
            WebUtility.HtmlEncode(message.ToStageName));

        await _emailSender.SendAsync(
            message.CandidateEmail,
            _emailText.Get(EmailTextKeys.Application.StageChangedSubject, language),
            body,
            ct);
    }
}
