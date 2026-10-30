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
            entity.Property(t => t.Description).HasMaxLength(Tenant.DescriptionMaxLength);
            entity.Property(t => t.Website).HasMaxLength(Tenant.WebsiteMaxLength);
            entity.Property(t => t.Location).HasMaxLength(Tenant.LocationMaxLength);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            // Older users signed up when all mail was English, hence the default.
            entity.Property(u => u.PreferredLanguage)
                .IsRequired()
                .HasMaxLength(2)
                .HasDefaultValue(SupportedLanguages.Default);
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

        // Called through an instance method so EF re-reads _currentTenant on every query.
        var applyFilter = GetType()
            .GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)))
        {
            applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    // EF allows one filter per entity, so tenant and soft delete share one predicate.
    private void SetTenantFilter<T>(ModelBuilder builder) where T : class, ITenantScoped, ISoftDeletable
        => builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && (Guid?)e.TenantId == _currentTenant.TenantId);
}
