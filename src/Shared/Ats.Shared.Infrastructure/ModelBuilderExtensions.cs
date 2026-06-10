using System.Linq.Expressions;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Shared.Infrastructure;

public static class ModelBuilderExtensions
{
    public static void ApplyTenantFilter(this ModelBuilder builder, ICurrentTenant currentTenant)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProperty = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
            var currentTenantId = Expression.Property(
                Expression.Constant(currentTenant), nameof(ICurrentTenant.TenantId));
            var currentTenantValue = Expression.Property(currentTenantId, "Value");

            var body = Expression.Equal(tenantIdProperty, currentTenantValue);
            var lambda = Expression.Lambda(body, parameter);

            builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
