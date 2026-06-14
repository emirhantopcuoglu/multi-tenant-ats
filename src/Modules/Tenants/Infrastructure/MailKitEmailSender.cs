using Ats.Modules.Tenants.Application;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public MailKitEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();

        // MailHog (local) accepts plaintext on None; a real SMTP server upgrades via StartTls.
        // The only difference between environments is configuration, not code.
        var socketOptions = _options.UseSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
