using System.Linq.Expressions;
using System.Reflection;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Shared.Infrastructure;

public static class ModelBuilderExtensions
{
    // For DbContexts that cannot use the instance-method pattern (e.g. IdentityDbContext subclasses),
    // this overload accepts a delegate so EF Core 9 evaluates it at query runtime, not compile time.
    public static void ApplyTenantFilter(this ModelBuilder builder, Func<Guid?> getCurrentTenantId)
    {
        var invokeMethod = typeof(Func<Guid?>).GetMethod("Invoke")!;
        var funcConstant = Expression.Constant(getCurrentTenantId);
        var invocation = Expression.Call(funcConstant, invokeMethod);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProperty = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
            var tenantIdAsNullable = Expression.Convert(tenantIdProperty, typeof(Guid?));
            var body = Expression.Equal(tenantIdAsNullable, invocation);
            var lambda = Expression.Lambda(body, parameter);

            builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }

    // For DbContexts that define a private SetTenantFilter<T> instance method,
    // this helper invokes it for every ITenantScoped entity type found in the model.
    public static void ApplyTenantFiltersViaInstanceMethod(
        this ModelBuilder builder,
        object dbContext,
        string methodName = "SetTenantFilter")
    {
        var method = dbContext.GetType()
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

        if (method is null)
            return;

        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)))
        {
            method.MakeGenericMethod(entityType.ClrType).Invoke(dbContext, [builder]);
        }
    }
}
