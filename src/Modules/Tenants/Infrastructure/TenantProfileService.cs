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

    // Tenant isn't tenant-scoped, so the id from the token is used explicitly.
    private async Task<Tenant?> FindCurrentTenantAsync(CancellationToken cancellationToken)
    {
        if (_currentTenant.TenantId is not { } tenantId)
            return null;

        return await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
    }

    // Returns typed errors; the entity's guards would throw and become a 500.
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

    // Rendered as a link on the careers page, so it must be an absolute http(s) URL.
    private static bool IsAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static TenantProfileDto ToDto(Tenant tenant) => new(
        tenant.Name, tenant.Slug, tenant.Description, tenant.Website, tenant.Location);
}
