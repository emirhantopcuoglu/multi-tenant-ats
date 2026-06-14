using System.Reflection;
using Ats.Modules.Tenants.Domain;
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

    public DbSet<Invitation> Invitations => Set<Invitation>();

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

        builder.Entity<Invitation>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Email).IsRequired().HasMaxLength(256);
            entity.Property(i => i.Role).IsRequired().HasMaxLength(50);
            entity.Property(i => i.TokenHash).IsRequired();
            entity.HasIndex(i => i.TokenHash);
        });

        // Register filter via instance method so EF Core 9 treats _currentTenant as a
        // context accessor (re-evaluated per execution) rather than a compile-time constant.
        var applyFilter = GetType()
            .GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)))
        {
            applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    // Combined global filter: hide rows of other tenants AND soft-deleted rows. EF Core
    // allows only one query filter per entity, so both conditions live in one predicate.
    // Written as a literal lambda in an instance method so EF re-evaluates _currentTenant
    // per query (a captured delegate would bake the first scope's tenant into the model).
    private void SetTenantFilter<T>(ModelBuilder builder) where T : class, ITenantScoped, ISoftDeletable
        => builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && (Guid?)e.TenantId == _currentTenant.TenantId);
}
