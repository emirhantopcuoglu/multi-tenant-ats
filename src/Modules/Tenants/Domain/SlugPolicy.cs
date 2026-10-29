using System.Text.RegularExpressions;
using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Domain;

// The slug is part of public URLs (/{slug}/jobs), so it must be URL-safe and must not shadow
// an app or API route.
public static partial class SlugPolicy
{
    public const int MinLength = 2;
    public const int MaxLength = 40;

    // Mirrors the frontend's SLUG_PATTERN.
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugFormat();

    // SPA top-level routes (a matching slug would be shadowed by them), then API prefixes.
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "login", "register", "accept-invitation", "playground",
        "forgot-password", "reset-password", "confirm-email",
        "jobs", "applications", "interviews", "candidates", "settings",
        "api", "health", "hangfire", "metrics", "swagger", "public",
    };

    // Expects a trimmed, lower-cased slug.
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
