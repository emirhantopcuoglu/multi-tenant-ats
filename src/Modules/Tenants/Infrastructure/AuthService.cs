using System.Security.Cryptography;
using System.Text;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TenantsDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly ICurrentTenant _currentTenant;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        TenantsDbContext db,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        ICurrentTenant currentTenant)
    {
        _userManager = userManager;
        _db = db;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _currentTenant = currentTenant;
    }

    public async Task<Result<AuthResult>> RegisterAsync(
        string companyName, string slug, string email, string password, string firstName, string lastName)
    {
        // Normalize once, then validate and check uniqueness against that exact value. The slug is
        // stored lower-cased, so comparing the raw input would let "Acme" pass the uniqueness check
        // against a stored "acme" and then collide on insert.
        var normalizedSlug = (slug ?? string.Empty).Trim().ToLowerInvariant();

        var slugValidation = SlugPolicy.Validate(normalizedSlug);
        if (slugValidation.IsFailure)
            return Result.Failure<AuthResult>(slugValidation.Error);

        var slugTaken = await _db.Tenants.AnyAsync(t => t.Slug == normalizedSlug);
        if (slugTaken)
            return Result.Failure<AuthResult>(AuthErrors.RegistrationFailed($"Slug '{normalizedSlug}' is already taken."));

        var emailTaken = await _userManager.FindByEmailAsync(email) is not null;
        if (emailTaken)
            return Result.Failure<AuthResult>(AuthErrors.RegistrationFailed($"Email '{email}' is already registered."));

        var tenant = Tenant.Create(companyName, normalizedSlug);
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            TenantId = tenant.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        var identityResult = await _userManager.CreateAsync(user, password);
        if (!identityResult.Succeeded)
            return Result.Failure<AuthResult>(
                AuthErrors.RegistrationFailed(string.Join("; ", identityResult.Errors.Select(e => e.Description))));

        // The user who registers a tenant is its founder and therefore its administrator.
        await _userManager.AddToRoleAsync(user, Roles.Admin);

        var tokens = await IssueTokensAsync(user);
        return Result.Success(tokens);
    }

    public async Task<Result<AuthResult>> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, password))
            return Result.Failure<AuthResult>(AuthErrors.InvalidCredentials);

        var tokens = await IssueTokensAsync(user);
        return Result.Success(tokens);
    }

    public async Task<Result<AuthResult>> RefreshAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is null || !stored.IsActive)
            return Result.Failure<AuthResult>(AuthErrors.InvalidRefreshToken);

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            return Result.Failure<AuthResult>(AuthErrors.UserNotFound);

        stored.Revoke();
        await _db.SaveChangesAsync();

        var tokens = await IssueTokensAsync(user);
        return Result.Success(tokens);
    }

    public async Task<Result> LogoutAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is not null && stored.IsActive)
        {
            stored.Revoke();
            await _db.SaveChangesAsync();
        }

        return Result.Success();
    }

    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure<CurrentUserDto>(AuthErrors.UserNotFound);

        // Every user belongs to a tenant — register creates the tenant before the user, and invited
        // users accept into an existing one — so a missing TenantId/tenant is an inconsistent state,
        // not a normal case. Treat it as "user not found" rather than returning a half-built profile.
        var tenant = user.TenantId is { } tenantId
            ? await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId)
            : null;
        if (tenant is null)
            return Result.Failure<CurrentUserDto>(AuthErrors.UserNotFound);

        // We model one role per user (RegisterAsync assigns Admin; invitations assign a single role),
        // so the first role is the user's role.
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;

        var dto = new CurrentUserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            role,
            new CurrentUserTenantDto(tenant.Name, tenant.Slug));

        return Result.Success(dto);
    }

    public async Task<IReadOnlyList<TenantUserDto>> ListTenantUsersAsync()
    {
        if (_currentTenant.TenantId is not { } tenantId)
            return [];

        // One query joining the Identity role tables, rather than UserManager.GetRolesAsync per user
        // (which would be N+1). ApplicationUser is not ITenantScoped, so the tenant filter is explicit
        // here rather than coming from the global query filter. One role per user, so one row per user.
        var users = await (
            from user in _db.Users.AsNoTracking()
            where user.TenantId == tenantId
            join userRole in _db.UserRoles on user.Id equals userRole.UserId into userRoles
            from userRole in userRoles.DefaultIfEmpty()
            join role in _db.Roles on userRole.RoleId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            orderby user.FirstName, user.LastName
            select new TenantUserDto(
                user.Id, user.FirstName, user.LastName, user.Email!,
                role != null ? role.Name! : string.Empty))
            .ToListAsync();

        return users;
    }

    private async Task<AuthResult> IssueTokensAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);

        var refreshToken = _tokenService.GenerateRefreshToken();
        var entity = RefreshToken.Issue(
            user.Id,
            Hash(refreshToken),
            DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays));

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync();

        return new AuthResult(accessToken, refreshToken);
    }

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}
