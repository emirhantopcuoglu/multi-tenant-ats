using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public interface IInvitationService
{
    Task<Result> InviteAsync(string email, string role, CancellationToken ct = default);
    Task<Result> AcceptAsync(
        string token, string password, string firstName, string lastName, string preferredLanguage,
        CancellationToken ct = default);
}
