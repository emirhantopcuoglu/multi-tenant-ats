using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Emails the candidate a confirmation after they apply. Idempotent on the message id.
// The language comes from the candidate's account, looked up inside the guard.
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

        // Name and job title are user input going into HTML.
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
