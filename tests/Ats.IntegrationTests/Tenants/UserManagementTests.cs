using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.Tenants;

[Collection("Integration")]
public sealed class UserManagementTests : IAsyncLifetime
{
    // Generated at runtime rather than hardcoded so secret scanners don't flag a literal credential.
    // "Aa1!" satisfies Identity's default password policy; the GUID makes each run unique.
    private static readonly string TestPassword = $"Aa1!{Guid.NewGuid():N}";

    private readonly PostgresContainerFixture _fixture;
    private Guid _tenantId;

    public UserManagementTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var provider = BuildProvider(null, null);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"RefreshTokens\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUserRoles\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"AspNetUsers\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"Tenants\"");

        var tenant = Tenant.Create("Acme Inc", "acme");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- role change ----

    [Fact]
    public async Task ChangeRole_should_replace_the_single_role()
    {
        // Arrange — an admin acting on a recruiter
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var target = await SeedUserAsync("rec@acme.test", Roles.Recruiter);

        // Act
        var result = await Act(admin, s => s.ChangeRoleAsync(target, Roles.HiringManager));

        // Assert — exactly one role, the new one
        Assert.True(result.IsSuccess);
        Assert.Equal([Roles.HiringManager], await RolesOfAsync(target));
    }

    [Fact]
    public async Task ChangeRole_should_reject_an_unknown_role()
    {
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var target = await SeedUserAsync("rec@acme.test", Roles.Recruiter);

        var result = await Act(admin, s => s.ChangeRoleAsync(target, "Superuser"));

        Assert.True(result.IsFailure);
        Assert.Equal(UserManagementErrors.InvalidRole.Code, result.Error.Code);
        Assert.Equal([Roles.Recruiter], await RolesOfAsync(target));
    }

    [Fact]
    public async Task ChangeRole_should_refuse_to_demote_the_last_admin()
    {
        // The tenant would be left with nobody who can manage users, invite anyone, or edit the
        // company profile — unrecoverable from inside the product.
        var caller = ACallerWhoseRoleClaimIsStale();
        var soleAdmin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var recruiter = await SeedUserAsync("rec@acme.test", Roles.Recruiter);

        // Demoting a non-admin is unaffected by the rule.
        Assert.True((await Act(caller, s => s.ChangeRoleAsync(recruiter, Roles.ReadOnly))).IsSuccess);

        var result = await Act(caller, s => s.ChangeRoleAsync(soleAdmin, Roles.ReadOnly));

        Assert.True(result.IsFailure);
        Assert.Equal(UserManagementErrors.LastAdmin.Code, result.Error.Code);
        Assert.Equal([Roles.Admin], await RolesOfAsync(soleAdmin));
    }

    [Fact]
    public async Task Demoting_one_of_two_admins_should_be_allowed()
    {
        var caller = ACallerWhoseRoleClaimIsStale();
        var first = await SeedUserAsync("admin@acme.test", Roles.Admin);
        await SeedUserAsync("other@acme.test", Roles.Admin);

        var result = await Act(caller, s => s.ChangeRoleAsync(first, Roles.ReadOnly));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_deactivated_admin_should_not_count_towards_the_last_admin_check()
    {
        // A deactivated admin cannot sign in, so leaving one behind locks the tenant out just as
        // effectively as having none — which is why the check filters on DeactivatedAtUtc.
        var caller = ACallerWhoseRoleClaimIsStale();
        var remaining = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var spare = await SeedUserAsync("spare@acme.test", Roles.Admin);

        // With two active admins, deactivating one is fine.
        Assert.True((await Act(caller, s => s.DeactivateAsync(spare))).IsSuccess);

        // Now the only other admin is deactivated, so this one is effectively the last.
        var result = await Act(caller, s => s.DeactivateAsync(remaining));

        Assert.True(result.IsFailure);
        Assert.Equal(UserManagementErrors.LastAdmin.Code, result.Error.Code);
    }

    [Fact]
    public async Task ChangeRole_should_refuse_to_target_the_caller()
    {
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        await SeedUserAsync("spare@acme.test", Roles.Admin);

        // Not the last-admin rule — there is a spare. An Admin demoting themselves by accident is its
        // own footgun, so it is refused regardless.
        var result = await Act(admin, s => s.ChangeRoleAsync(admin, Roles.ReadOnly));

        Assert.True(result.IsFailure);
        Assert.Equal(UserManagementErrors.CannotTargetSelf.Code, result.Error.Code);
    }

    [Fact]
    public async Task ChangeRole_should_reject_a_user_from_another_tenant()
    {
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var outsider = await SeedUserInOtherTenantAsync();

        var result = await Act(admin, s => s.ChangeRoleAsync(outsider, Roles.ReadOnly));

        // NotFound rather than Forbidden: an Admin has no business learning that the id exists at all.
        Assert.True(result.IsFailure);
        Assert.Equal(UserManagementErrors.NotFound.Code, result.Error.Code);
    }

    // ---- deactivation ----

    [Fact]
    public async Task Deactivate_should_stop_the_user_signing_in()
    {
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var target = await SeedUserAsync("rec@acme.test", Roles.Recruiter);

        await Act(admin, s => s.DeactivateAsync(target));

        var login = await LoginAsync("rec@acme.test");
        Assert.True(login.IsFailure);
        // Indistinguishable from a wrong password on purpose.
        Assert.Equal(AuthErrors.InvalidCredentials.Code, login.Error.Code);
    }

    [Fact]
    public async Task Deactivate_should_revoke_the_users_refresh_token_rows()
    {
        // Asserted on the rows, not on "refresh now fails": AuthService.RefreshAsync also rejects an
        // inactive user, so a behavioural assertion alone passes even with the revocation removed and
        // would not notice it disappearing. The revocation is what makes the cutoff immediate rather
        // than leaving live rows that only the second guard stops.
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        await SeedUserAsync("rec@acme.test", Roles.Recruiter);
        await LoginAsync("rec@acme.test");
        await LoginAsync("rec@acme.test");
        var target = await UserIdOfAsync("rec@acme.test");

        Assert.Equal(2, await CountActiveRefreshTokensAsync(target));

        await Act(admin, s => s.DeactivateAsync(target));

        Assert.Equal(0, await CountActiveRefreshTokensAsync(target));
    }

    [Fact]
    public async Task A_deactivated_user_should_not_be_able_to_refresh()
    {
        // End to end through the real flow. Note this passes on the revocation alone, so it does NOT
        // cover the RefreshAsync guard — that is what the next test is for.
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        await SeedUserAsync("rec@acme.test", Roles.Recruiter);
        var session = await LoginAsync("rec@acme.test");
        var target = await UserIdOfAsync("rec@acme.test");

        await Act(admin, s => s.DeactivateAsync(target));

        var refresh = await RefreshAsync(session.Value.RefreshToken);
        Assert.True(refresh.IsFailure);
        Assert.Equal(AuthErrors.InvalidRefreshToken.Code, refresh.Error.Code);
    }

    [Fact]
    public async Task An_unrevoked_token_belonging_to_a_deactivated_user_should_still_be_refused()
    {
        // Isolates AuthService.RefreshAsync's own inactive-user guard, which the test above cannot
        // reach: deactivation revokes the rows first, so that path fails on the revocation and the
        // guard is never consulted. Deleting the guard therefore broke nothing — the exact blind spot
        // that already bit the revocation test, in mirror image.
        //
        // The flag is set straight in the database rather than through DeactivateAsync, which is the
        // whole point: it reproduces a live row that escaped the sweep — a login racing the
        // deactivation, or a row written before the sweep existed.
        await SeedUserAsync("rec@acme.test", Roles.Recruiter);
        var session = await LoginAsync("rec@acme.test");
        var target = await UserIdOfAsync("rec@acme.test");

        await using (var provider = BuildProvider(_tenantId, null))
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == target);
            user.DeactivatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // The token is still active — nothing revoked it — so only the guard can refuse this.
        Assert.Equal(1, await CountActiveRefreshTokensAsync(target));

        var refresh = await RefreshAsync(session.Value.RefreshToken);

        Assert.True(refresh.IsFailure);
        Assert.Equal(AuthErrors.InvalidRefreshToken.Code, refresh.Error.Code);
    }

    [Fact]
    public async Task Deactivate_should_leave_other_users_sessions_alone()
    {
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        await SeedUserAsync("rec@acme.test", Roles.Recruiter);
        await SeedUserAsync("other@acme.test", Roles.Recruiter);
        var otherSession = await LoginAsync("other@acme.test");
        var target = await UserIdOfAsync("rec@acme.test");

        await Act(admin, s => s.DeactivateAsync(target));

        var refresh = await RefreshAsync(otherSession.Value.RefreshToken);
        Assert.True(refresh.IsSuccess);
    }

    [Fact]
    public async Task Deactivate_should_refuse_to_target_the_caller()
    {
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        await SeedUserAsync("spare@acme.test", Roles.Admin);

        var result = await Act(admin, s => s.DeactivateAsync(admin));

        Assert.True(result.IsFailure);
        Assert.Equal(UserManagementErrors.CannotTargetSelf.Code, result.Error.Code);
    }

    [Fact]
    public async Task Deactivating_twice_should_report_that_nothing_changed()
    {
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var target = await SeedUserAsync("rec@acme.test", Roles.Recruiter);

        await Act(admin, s => s.DeactivateAsync(target));
        var second = await Act(admin, s => s.DeactivateAsync(target));

        Assert.True(second.IsFailure);
        Assert.Equal(UserManagementErrors.AlreadyInThatState.Code, second.Error.Code);
    }

    // ---- reactivation ----

    [Fact]
    public async Task Reactivate_should_let_the_user_sign_in_again_with_their_existing_password()
    {
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var target = await SeedUserAsync("rec@acme.test", Roles.Recruiter);
        await Act(admin, s => s.DeactivateAsync(target));

        var result = await Act(admin, s => s.ReactivateAsync(target));

        Assert.True(result.IsSuccess);
        Assert.True((await LoginAsync("rec@acme.test")).IsSuccess);
    }

    [Fact]
    public async Task Reactivate_should_report_that_nothing_changed_for_an_active_user()
    {
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var target = await SeedUserAsync("rec@acme.test", Roles.Recruiter);

        var result = await Act(admin, s => s.ReactivateAsync(target));

        Assert.True(result.IsFailure);
        Assert.Equal(UserManagementErrors.AlreadyInThatState.Code, result.Error.Code);
    }

    // ---- listing ----

    [Fact]
    public async Task The_user_list_should_keep_showing_a_deactivated_member_flagged_inactive()
    {
        // If they vanished, an Admin would have no way to reactivate them from the UI.
        var admin = await SeedUserAsync("admin@acme.test", Roles.Admin);
        var target = await SeedUserAsync("rec@acme.test", Roles.Recruiter);
        await Act(admin, s => s.DeactivateAsync(target));

        await using var provider = BuildProvider(_tenantId, admin);
        using var scope = provider.CreateScope();
        var users = await CreateAuthService(scope).ListTenantUsersAsync();

        var listed = Assert.Single(users, u => u.Id == target);
        Assert.False(listed.IsActive);
        Assert.True(Assert.Single(users, u => u.Id == admin).IsActive);
    }

    // ---- helpers ----

    // Runs an operation as a given caller. The service reads the caller from ICurrentUser, so the
    // acting identity is injected rather than passed to the method.
    private async Task<Result> Act(Guid callerId, Func<IUserManagementService, Task<Result>> operation)
    {
        await using var provider = BuildProvider(_tenantId, callerId);
        using var scope = provider.CreateScope();
        return await operation(new UserManagementService(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<TenantsDbContext>(),
            new FixedTenant(_tenantId),
            new FixedCurrentUser(callerId),
            NullLogger<UserManagementService>.Instance));
    }

    // A caller id belonging to nobody. Needed to reach the last-admin guard at all: the endpoint's
    // Admin policy means the caller is normally an active Admin, and since they cannot target
    // themselves, excluding the target still leaves them — so the guard never fires through the happy
    // path. It is still the right guard to have, because a role claim can be stale: an Admin demoted
    // five minutes ago still carries Admin in their unexpired access token. These tests exercise the
    // service directly, which is the layer that has to hold when the claim lies.
    private static Guid ACallerWhoseRoleClaimIsStale() => Guid.NewGuid();

    private async Task<Result<AuthResult>> LoginAsync(string email)
    {
        await using var provider = BuildProvider(_tenantId, null);
        using var scope = provider.CreateScope();
        return await CreateAuthService(scope).LoginAsync(email, TestPassword);
    }

    private async Task<Result<AuthResult>> RefreshAsync(string refreshToken)
    {
        await using var provider = BuildProvider(_tenantId, null);
        using var scope = provider.CreateScope();
        return await CreateAuthService(scope).RefreshAsync(refreshToken);
    }

    private async Task<Guid> SeedUserAsync(string email, string role)
    {
        await using var provider = BuildProvider(_tenantId, null);
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await RoleSeeder.SeedAsync(roleManager);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = email.Split('@')[0],
            LastName = "User",
            TenantId = _tenantId,
            // These suites are about deactivation and password recovery, not email confirmation, so
            // their users are seeded the way a real one looks after confirming. Without it the login
            // guard added with company email confirmation refuses them and every assertion below is
            // about the wrong thing.
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        Assert.True((await userManager.CreateAsync(user, TestPassword)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        return user.Id;
    }

    private async Task<Guid> SeedUserInOtherTenantAsync()
    {
        await using var provider = BuildProvider(_tenantId, null);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var otherTenant = Tenant.Create("Other Inc", "other");
        db.Tenants.Add(otherTenant);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = "outsider@other.test",
            Email = "outsider@other.test",
            FirstName = "Out",
            LastName = "Sider",
            TenantId = otherTenant.Id,
            // These suites are about deactivation and password recovery, not email confirmation, so
            // their users are seeded the way a real one looks after confirming. Without it the login
            // guard added with company email confirmation refuses them and every assertion below is
            // about the wrong thing.
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        Assert.True((await userManager.CreateAsync(user, TestPassword)).Succeeded);
        return user.Id;
    }

    private async Task<int> CountActiveRefreshTokensAsync(Guid userId)
    {
        await using var provider = BuildProvider(_tenantId, null);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        return await db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAtUtc == null);
    }

    private async Task<Guid> UserIdOfAsync(string email)
    {
        await using var provider = BuildProvider(_tenantId, null);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    private async Task<IReadOnlyList<string>> RolesOfAsync(Guid userId)
    {
        await using var provider = BuildProvider(_tenantId, null);
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return (await userManager.GetRolesAsync(user!)).OrderBy(r => r).ToList();
    }

    private static AuthService CreateAuthService(IServiceScope scope) =>
        new(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<TenantsDbContext>(),
            new RandomTokenService(),
            Options.Create(new JwtOptions { RefreshTokenDays = 7 }),
            Options.Create(new PasswordResetOptions()),
            Options.Create(new EmailConfirmationOptions()),
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>(),
            new NoOpEmailSender(),
            NullLogger<AuthService>.Instance);

    private ServiceProvider BuildProvider(Guid? tenantId, Guid? currentUserId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentTenant>(new FixedTenant(tenantId));
        services.AddSingleton<ICurrentUser>(new FixedCurrentUser(currentUserId));
        services.AddDbContext<TenantsDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TenantsDbContext>();
        return services.BuildServiceProvider();
    }

    // Real random refresh tokens, because these tests redeem them.
    private sealed class RandomTokenService : ITokenService
    {
        public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles) => string.Empty;
        public string GenerateRefreshToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
