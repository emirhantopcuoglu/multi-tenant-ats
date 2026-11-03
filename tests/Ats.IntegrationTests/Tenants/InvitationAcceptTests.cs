using System.Text.RegularExpressions;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.Tenants;

// Accept runs the way the app wires it: the service and UserManager share one scoped DbContext.
[Collection("Integration")]
public sealed class InvitationAcceptTests
{
    private static readonly string Password = $"Aa1!{Guid.NewGuid():N}";

    private readonly PostgresContainerFixture _fixture;

    public InvitationAcceptTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task parallel_accepts_of_one_link_should_create_one_user_and_refuse_the_rest()
    {
        await using var provider = BuildProvider();
        await EnsureRoleAsync(provider, Roles.Recruiter);
        var (email, token) = await InviteAsync(Roles.Recruiter);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => AcceptAsync(provider, token)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.All(results.Where(r => r.IsFailure),
            r => Assert.Equal(InvitationErrors.InvalidToken.Code, r.Error.Code));
        Assert.Equal(1, await UserCountAsync(email));
    }

    [Fact]
    public async Task a_failure_after_the_user_is_saved_should_leave_the_link_usable()
    {
        await using var provider = BuildProvider();
        await EnsureRoleAsync(provider, Roles.Recruiter);
        var (email, token) = await InviteAsync(Roles.Recruiter);

        // AddToRoleAsync throws for an unknown role, standing in for any failure after the user insert.
        await SetInvitationRoleAsync(email, "missing-role");
        await Assert.ThrowsAnyAsync<Exception>(() => AcceptAsync(provider, token));

        Assert.Equal(0, await UserCountAsync(email));
        await SetInvitationRoleAsync(email, Roles.Recruiter);
        var retry = await AcceptAsync(provider, token);
        Assert.True(retry.IsSuccess, retry.IsFailure ? retry.Error.Message : "");
    }

    private async Task<(string Email, string Token)> InviteAsync(string role)
    {
        var tenantId = await SeedTenantAsync();
        var email = $"invitee-{Guid.NewGuid():N}@acme.test";
        var mail = new RecordingEmailSender();
        var tenant = new FixedTenant(tenantId);
        await using var db = new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var invited = await CreateService(db, scope, mail).InviteAsync(email, role);

        Assert.True(invited.IsSuccess);
        var match = Regex.Match(mail.Sent[^1].Body, @"[?&]token=([^""&\s<]+)");
        Assert.True(match.Success, "The invitation mail did not contain a token link.");
        return (email, Uri.UnescapeDataString(match.Groups[1].Value));
    }

    private static async Task<Result> AcceptAsync(ServiceProvider provider, string token)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        return await CreateService(db, scope).AcceptAsync(token, Password, "New", "Colleague", SupportedLanguages.Default);
    }

    private static InvitationService CreateService(
        TenantsDbContext db, IServiceScope scope, IEmailSender? mail = null) =>
        new(db,
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            mail ?? new NoOpEmailSender(),
            new JsonEmailTextProvider(),
            new NullCurrentUser(),
            Options.Create(new InvitationOptions()));

    private async Task SetInvitationRoleAsync(string email, string role)
    {
        await using var db = NewDb();
        await db.Invitations.IgnoreQueryFilters()
            .Where(i => i.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Role, role));
    }

    private async Task<int> UserCountAsync(string email)
    {
        await using var db = NewDb();
        return await db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == email);
    }

    private static async Task EnsureRoleAsync(ServiceProvider provider, string role)
    {
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
    }

    private async Task<Guid> SeedTenantAsync()
    {
        await using var db = NewDb();
        var tenant = Tenant.Create("Acme", $"acme-{Guid.NewGuid():N}"[..20]);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    // The accept endpoint is anonymous, so the scoped context has no tenant, as in the app.
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentTenant>(new FixedTenant(null));
        services.AddDbContext<TenantsDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TenantsDbContext>();
        return services.BuildServiceProvider();
    }

    private TenantsDbContext NewDb() =>
        new(PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, new FixedTenant(null)),
            new FixedTenant(null));
}
