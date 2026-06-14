namespace Ats.Modules.Tenants.Application;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
