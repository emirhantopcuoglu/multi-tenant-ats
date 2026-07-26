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

    // Distinct from InvalidCredentials on purpose, and safe to be distinct: login only reaches this
    // check after the password has already verified, so the caller has proved they own the account.
    // Telling them to check their inbox reveals nothing they do not already know, and the UI needs the
    // code to offer a resend.
    //
    // Contrast the deactivated-user case, which deliberately answers exactly like a wrong password:
    // there the hidden fact is someone's employment status, which a login form has no business
    // disclosing to whoever happens to hold the password.
    public static readonly Error EmailNotConfirmed =
        new("auth.email_not_confirmed",
            "Confirm your email address before signing in. Check your inbox for the confirmation link.");

    // Same shape as InvalidPasswordResetToken and for the same reason: the caller holds only a link, so
    // unknown / malformed / expired / already-used must be one answer.
    public static readonly Error InvalidEmailConfirmationToken =
        new("auth.invalid_email_confirmation_token",
            "This confirmation link is invalid, expired or already used.");

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
