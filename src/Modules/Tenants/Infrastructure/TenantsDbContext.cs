using Ats.Modules.Tenants.Domain;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class TenantsDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentTenant _currentTenant;

    public TenantsDbContext(DbContextOptions<TenantsDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("tenants");

        builder.Entity<Tenant>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Slug).IsRequired().HasMaxLength(100);
            entity.HasIndex(t => t.Slug).IsUnique();
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).IsRequired();
            entity.HasIndex(t => t.TokenHash);
        });

        builder.ApplyTenantFilter(_currentTenant);
    }
}
