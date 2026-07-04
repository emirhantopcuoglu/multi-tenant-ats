using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

// The tenant's own view of its public profile, for the Settings screen. Name and slug ride along
// read-only (they are set at registration and have no update path); the optional fields are what
// the admin edits here and what the public careers page renders.
public sealed record TenantProfileDto(
    string CompanyName,
    string Slug,
    string? Description,
    string? Website,
    string? Location);

public sealed record UpdateTenantProfileRequest(
    string? Description,
    string? Website,
    string? Location);

// Follows the module's service convention (IAuthService, IInvitationService) rather than CQRS —
// the Tenants module has no MediatR pipeline, and two methods do not justify introducing one.
public interface ITenantProfileService
{
    Task<Result<TenantProfileDto>> GetAsync(CancellationToken cancellationToken = default);
    Task<Result<TenantProfileDto>> UpdateAsync(
        UpdateTenantProfileRequest request, CancellationToken cancellationToken = default);
}

public static class TenantProfileErrors
{
    public static readonly Error TenantNotFound =
        new("tenant_profile.not_found", "The tenant for this account no longer exists.");

    public static readonly Error DescriptionTooLong =
        new("tenant_profile.description_too_long", "Description is too long.");

    public static readonly Error WebsiteTooLong =
        new("tenant_profile.website_too_long", "Website is too long.");

    public static readonly Error LocationTooLong =
        new("tenant_profile.location_too_long", "Location is too long.");

    public static readonly Error WebsiteNotAnAbsoluteHttpUrl =
        new("tenant_profile.website_invalid", "Website must be a full http(s) URL.");
}
