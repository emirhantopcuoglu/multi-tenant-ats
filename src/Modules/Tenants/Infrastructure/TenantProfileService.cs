using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class TenantProfileService : ITenantProfileService
{
    private readonly TenantsDbContext _db;
    private readonly ICurrentTenant _currentTenant;

    public TenantProfileService(TenantsDbContext db, ICurrentTenant currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<Result<TenantProfileDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await FindCurrentTenantAsync(cancellationToken);
        return tenant is null
            ? Result.Failure<TenantProfileDto>(TenantProfileErrors.TenantNotFound)
            : Result.Success(ToDto(tenant));
    }

    public async Task<Result<TenantProfileDto>> UpdateAsync(
        UpdateTenantProfileRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return Result.Failure<TenantProfileDto>(validationError);

        var tenant = await FindCurrentTenantAsync(cancellationToken);
        if (tenant is null)
            return Result.Failure<TenantProfileDto>(TenantProfileErrors.TenantNotFound);

        tenant.UpdateProfile(request.Description, request.Website, request.Location);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(tenant));
    }

    // The Tenant entity is the scope root, not a tenant-scoped row, so there is no global query
    // filter doing this for us — the caller's tenant id from the token is the explicit key.
    private async Task<Tenant?> FindCurrentTenantAsync(CancellationToken cancellationToken)
    {
        if (_currentTenant.TenantId is not { } tenantId)
            return null;

        return await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
    }

    // Boundary validation returning typed errors (the entity's own guard throws, which would surface
    // as a 500). Checked against the trimmed value: trailing whitespace should not fail a length cap
    // when the entity is about to trim it away anyway.
    private static Error? Validate(UpdateTenantProfileRequest request)
    {
        if (request.Description?.Trim().Length > Tenant.DescriptionMaxLength)
            return TenantProfileErrors.DescriptionTooLong;

        if (request.Website?.Trim().Length > Tenant.WebsiteMaxLength)
            return TenantProfileErrors.WebsiteTooLong;

        if (request.Location?.Trim().Length > Tenant.LocationMaxLength)
            return TenantProfileErrors.LocationTooLong;

        if (!string.IsNullOrWhiteSpace(request.Website) && !IsAbsoluteHttpUrl(request.Website.Trim()))
            return TenantProfileErrors.WebsiteNotAnAbsoluteHttpUrl;

        return null;
    }

    // Same rule as the apply form's LinkedIn field: the value is rendered as a link on the public
    // careers page, and a non-absolute or non-http(s) value would produce a broken or unsafe href.
    private static bool IsAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static TenantProfileDto ToDto(Tenant tenant) => new(
        tenant.Name, tenant.Slug, tenant.Description, tenant.Website, tenant.Location);
}
