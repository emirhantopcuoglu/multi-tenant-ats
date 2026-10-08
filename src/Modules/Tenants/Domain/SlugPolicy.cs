using System.Text.RegularExpressions;
using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Domain;

// Validates a tenant slug at registration. The slug becomes part of public, candidate-facing URLs
// (/{slug}/jobs), so it must be URL-safe and must not collide with a reserved application or API
// route prefix — otherwise the careers page would be shadowed by another route, or a malformed slug
// would break tenant resolution. Pure and dependency-free (only Result/Error), so the rule is unit-
// testable in isolation and enforced at the boundary instead of trusting the caller.
public static partial class SlugPolicy
{
    public const int MinLength = 2;
    public const int MaxLength = 40;

    // Lowercase alphanumeric, hyphen-separated, no leading/trailing/double hyphens. Mirrors the
    // front-end's SLUG_PATTERN so both ends agree on what a valid slug is — but this is the
    // authoritative check; the client's is only for instant feedback.
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugFormat();

    // Route prefixes a slug must never take. The first row are the SPA's top-level static routes (a
    // slug equal to one is shadowed by that route in React Router, so its careers page is
    // unreachable); the second are API/infrastructure prefixes.
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "login", "register", "accept-invitation", "playground",
        "forgot-password", "reset-password", "confirm-email",
        "jobs", "applications", "interviews", "candidates", "settings",
        "api", "health", "hangfire", "metrics", "swagger", "public",
    };

    // Expects an already-normalized (trimmed, lower-cased) slug — registration normalizes once and
    // validates that exact value so the stored slug is what was checked.
    public static Result Validate(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return Result.Failure(SlugErrors.Required);

        if (slug.Length is < MinLength or > MaxLength)
            return Result.Failure(SlugErrors.InvalidLength);

        if (!SlugFormat().IsMatch(slug))
            return Result.Failure(SlugErrors.InvalidFormat);

        return Reserved.Contains(slug)
            ? Result.Failure(SlugErrors.Reserved)
            : Result.Success();
    }
}

public static class SlugErrors
{
    public static readonly Error Required =
        new("tenant.slug_required", "A workspace URL is required.");

    public static readonly Error InvalidLength =
        new("tenant.slug_invalid_length", $"The workspace URL must be {SlugPolicy.MinLength}–{SlugPolicy.MaxLength} characters.");

    public static readonly Error InvalidFormat =
        new("tenant.slug_invalid_format", "The workspace URL may contain only lowercase letters, numbers, and hyphens.");

    public static readonly Error Reserved =
        new("tenant.slug_reserved", "That workspace URL is reserved. Please choose another.");
}
