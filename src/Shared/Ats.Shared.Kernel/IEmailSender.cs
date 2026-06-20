namespace Ats.Shared.Kernel;

// A port for sending transactional email, shared across modules: the Tenants module sends user
// invitations, the Notifications module sends application emails. Email is a cross-cutting
// infrastructure capability, not a single module's concern, so — like IFileStorage — the
// abstraction lives in the kernel and the SMTP implementation lives in shared infrastructure.
// This keeps modules from depending on each other just to send mail.
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
