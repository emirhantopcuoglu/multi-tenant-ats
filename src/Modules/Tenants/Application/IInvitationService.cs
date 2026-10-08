using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public interface IInvitationService
{
    Task<Result> InviteAsync(string email, string role, CancellationToken ct = default);
    // preferredLanguage is the UI language on the accept page: the invitee has had no chance to store
    // one before this call, and the account it creates starts sending mail immediately.
    Task<Result> AcceptAsync(
        string token, string password, string firstName, string lastName, string preferredLanguage,
        CancellationToken ct = default);
}
