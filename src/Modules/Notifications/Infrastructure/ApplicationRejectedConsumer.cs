using System.Net;
using Ats.Shared.Contracts.CandidateAccounts;
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
//
// Wording comes from IEmailTextProvider in the language the candidate's account carries, which is
// why this consumer reads ICandidateAccountReader: the event identifies the recipient by address,
// and the language belongs to the account behind it. The lookup sits inside the idempotency guard,
// so a duplicate delivery costs no query.
public sealed class ApplicationRejectedConsumer : IConsumer<ApplicationRejectedIntegrationEvent>
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ICandidateAccountReader _candidateAccounts;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationRejectedConsumer> _logger;

    public ApplicationRejectedConsumer(
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ICandidateAccountReader candidateAccounts,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationRejectedConsumer> logger)
    {
        _emailSender = emailSender;
        _emailText = emailText;
        _candidateAccounts = candidateAccounts;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationRejectedIntegrationEvent> context)
    {
        var message = context.Message;

        var key = $"notifications:application-rejected:{context.MessageId}";
        var sent = await _idempotencyGuard.ProcessOnceAsync(
            key, () => SendAsync(message, context.CancellationToken));

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

    private async Task SendAsync(ApplicationRejectedIntegrationEvent message, CancellationToken ct)
    {
        var language = await _candidateAccounts.GetPreferredLanguageByEmailAsync(message.CandidateEmail, ct);

        // The candidate's name comes from the public apply form, so it is untrusted in an HTML email
        // and is encoded here; the job title is encoded inside RolePhrase for the same reason.
        var body = _emailText.Get(
            EmailTextKeys.Application.RejectedBody,
            language,
            WebUtility.HtmlEncode(message.CandidateFirstName),
            RolePhrase.For(message.JobTitle, _emailText, language));

        await _emailSender.SendAsync(
            message.CandidateEmail,
            _emailText.Get(EmailTextKeys.Application.RejectedSubject, language),
            body,
            ct);
    }
}
