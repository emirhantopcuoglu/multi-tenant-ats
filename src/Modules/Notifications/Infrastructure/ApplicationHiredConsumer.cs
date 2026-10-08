using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Consumes ApplicationHiredIntegrationEvent off RabbitMQ and emails the candidate the good news —
// the positive counterpart of ApplicationRejectedConsumer, with the same retry/dead-letter policy
// and the same idempotency guard keyed on the message id, so an at-least-once redelivery does not
// email the candidate twice.
//
// Wording comes from IEmailTextProvider in the language the candidate's account carries; see
// ApplicationRejectedConsumer for why the language is read here rather than carried on the event.
public sealed class ApplicationHiredConsumer : IConsumer<ApplicationHiredIntegrationEvent>
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ICandidateAccountReader _candidateAccounts;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationHiredConsumer> _logger;

    public ApplicationHiredConsumer(
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ICandidateAccountReader candidateAccounts,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationHiredConsumer> logger)
    {
        _emailSender = emailSender;
        _emailText = emailText;
        _candidateAccounts = candidateAccounts;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationHiredIntegrationEvent> context)
    {
        var message = context.Message;

        var key = $"notifications:application-hired:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key, () => SendAsync(message, context.CancellationToken));

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

    private async Task SendAsync(ApplicationHiredIntegrationEvent message, CancellationToken ct)
    {
        var language = await _candidateAccounts.GetPreferredLanguageByEmailAsync(message.CandidateEmail, ct);

        var body = _emailText.Get(
            EmailTextKeys.Application.HiredBody,
            language,
            WebUtility.HtmlEncode(message.CandidateFirstName),
            RolePhrase.For(message.JobTitle, _emailText, language));

        await _emailSender.SendAsync(
            message.CandidateEmail,
            _emailText.Get(EmailTextKeys.Application.HiredSubject, language),
            body,
            ct);
    }
}
