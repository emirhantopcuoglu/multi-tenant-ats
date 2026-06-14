using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public static class InvitationErrors
{
    public static readonly Error InvalidRole = new("invite.invalid_role", "The specified role is not valid.");
    public static readonly Error InvalidToken = new("invite.invalid_token", "The invitation is invalid or has expired.");
    public static readonly Error EmailInUse = new("invite.email_in_use", "A user with this email already exists.");
    public static Error CreationFailed(string detail) => new("invite.creation_failed", detail);
}
