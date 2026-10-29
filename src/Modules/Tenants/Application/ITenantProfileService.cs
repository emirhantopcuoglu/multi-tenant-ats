using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

// Name and slug are read-only; they are set at registration.
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
