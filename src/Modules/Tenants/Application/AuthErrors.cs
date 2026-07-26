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

    // One error for "unknown user", "bad token", "expired" and "already used" alike: the caller only
    // holds a link, so distinguishing the cases would tell an attacker which guesses were once real.
    // There is deliberately no "email not found" counterpart — requesting a reset for an unknown
    // address reports success, so the endpoint cannot enumerate accounts.
    public static readonly Error InvalidPasswordResetToken =
        new("auth.invalid_password_reset_token",
            "This password reset link is invalid, expired or already used.");

    // Wraps Identity's own password validation failures (length, and anything configured later) so the
    // API answers 400 with the concrete rule instead of a generic error.
    public static Error PasswordRejected(string detail) =>
        new("auth.password_rejected", detail);
}
