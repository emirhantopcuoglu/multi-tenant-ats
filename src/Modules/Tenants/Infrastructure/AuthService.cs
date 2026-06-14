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

    public AuthService(
        UserManager<ApplicationUser> userManager,
        TenantsDbContext db,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _db = db;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<AuthResult>> RegisterAsync(
        string companyName, string slug, string email, string password, string firstName, string lastName)
    {
        var slugTaken = await _db.Tenants.AnyAsync(t => t.Slug == slug);
        if (slugTaken)
            return Result.Failure<AuthResult>(AuthErrors.RegistrationFailed($"Slug '{slug}' is already taken."));

        var emailTaken = await _userManager.FindByEmailAsync(email) is not null;
        if (emailTaken)
            return Result.Failure<AuthResult>(AuthErrors.RegistrationFailed($"Email '{email}' is already registered."));

        var tenant = Tenant.Create(companyName, slug);
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
