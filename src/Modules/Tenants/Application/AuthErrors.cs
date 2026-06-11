using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "Invalid email or password.");

    public static readonly Error InvalidRefreshToken =
        new("auth.invalid_refresh_token", "The refresh token is invalid or expired.");

    public static readonly Error UserNotFound =
        new("auth.user_not_found", "User not found.");

    public static Error RegistrationFailed(string detail) =>
        new("auth.registration_failed", detail);
}
