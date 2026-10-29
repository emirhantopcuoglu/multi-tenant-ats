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

    public static readonly Error UnsupportedLanguage =
        new("auth.unsupported_language", "Language must be one of the supported languages.");

    // Safe to be distinct: login only reaches this after the password is verified.
    public static readonly Error EmailNotConfirmed =
        new("auth.email_not_confirmed",
            "Confirm your email address before signing in. Check your inbox for the confirmation link.");

    // One error for unknown, expired and used tokens, so guesses reveal nothing.
    public static readonly Error InvalidEmailConfirmationToken =
        new("auth.invalid_email_confirmation_token",
            "This confirmation link is invalid, expired or already used.");

    public static Error RegistrationFailed(string detail) =>
        new("auth.registration_failed", detail);

    // One error for unknown, expired and used tokens, so guesses reveal nothing.
    public static readonly Error InvalidPasswordResetToken =
        new("auth.invalid_password_reset_token",
            "This password reset link is invalid, expired or already used.");

    public static Error PasswordRejected(string detail) =>
        new("auth.password_rejected", detail);
}
