namespace Ats.Shared.Kernel;

// In the Kernel because several modules send email.
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
