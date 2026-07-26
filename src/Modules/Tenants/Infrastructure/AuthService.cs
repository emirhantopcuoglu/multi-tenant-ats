using System.Security.Cryptography;
using System.Text;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TenantsDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly PasswordResetOptions _passwordResetOptions;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        TenantsDbContext db,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        IOptions<PasswordResetOptions> passwordResetOptions,
        ICurrentTenant currentTenant,
        IEmailSender emailSender,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _db = db;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _passwordResetOptions = passwordResetOptions.Value;
        _currentTenant = currentTenant;
        _emailSender = emailSender;
        _logger = logger;
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

    public async Task<Result> RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email ?? string.Empty);

        if (user is null)
        {
            // Success, not a 404. This endpoint is callable by anyone, so a distinguishable answer
            // would turn it into a directory of who works here. Logged so the miss stays visible to an
            // operator without the caller learning anything.
            _logger.LogInformation("Password reset requested for an unregistered email");
            return Result.Success();
        }

        // Identity's own token: a signed, data-protected payload rather than a row we store. It embeds
        // the user's security stamp, so it stops verifying the moment the password changes — that is
        // what makes it single-use without a ConsumedAtUtc column like the candidate side needs.
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        _logger.LogInformation("Password reset requested for user {UserId}", user.Id);

        // Best-effort, unlike the invitation mail which fails loudly. That one answers an
        // authenticated admin who is owed a real result; this one must respond identically whether or
        // not the address exists, and a hard failure would leak that it does.
        await SendPasswordResetLinkAsync(user.Email!, user.Id, token, ct);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        Guid userId, string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(AuthErrors.InvalidPasswordResetToken);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure(AuthErrors.InvalidPasswordResetToken);

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword ?? string.Empty);
        if (!result.Succeeded)
        {
            // Identity lumps "bad token" in with "password too short", but the two must not answer the
            // same way: one is an invalid link the user cannot fix, the other is a rule they can. Only
            // the token failure collapses into the deliberately vague error.
            var isTokenFailure = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken));
            return isTokenFailure
                ? Result.Failure(AuthErrors.InvalidPasswordResetToken)
                : Result.Failure(AuthErrors.PasswordRejected(
                    string.Join("; ", result.Errors.Select(e => e.Description))));
        }

        // The candidate side gets this for free: its tokens carry a security stamp that is checked on
        // every request, so rotating it kills live sessions. Company tokens carry no stamp and nothing
        // validates one, so the refresh tokens have to be revoked by hand — otherwise resetting a
        // stolen password would leave the thief a refresh token good for its full RefreshTokenDays.
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var refreshToken in activeTokens)
            refreshToken.Revoke();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset completed for user {UserId}; revoked {RevokedCount} refresh token(s)",
            user.Id, activeTokens.Count);

        await NotifyPasswordResetAsync(user.Email!, ct);

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

    private async Task SendPasswordResetLinkAsync(
        string email, Guid userId, string token, CancellationToken ct)
    {
        // The user id, not the email, goes in the URL: the address would otherwise end up in browser
        // history, referrer headers and any proxy log the link passes through. Both values are escaped
        // because Identity's token is base64-ish and contains characters that are unsafe unencoded.
        var link = $"{_passwordResetOptions.ResetBaseUrl}" +
                   $"?userId={Uri.EscapeDataString(userId.ToString())}" +
                   $"&token={Uri.EscapeDataString(token)}";

        var body = $"""
            <p>A request was made to reset the password of your ATS account.</p>
            <p><a href="{link}">Choose a new password</a></p>
            <p>This link expires in {_passwordResetOptions.ValidMinutes} minutes and can be used once.
            If you did not request this, ignore this email — your current password still works.</p>
            """;

        try
        {
            await _emailSender.SendAsync(email, "Reset your password", body, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the password reset email");
        }
    }

    // Best-effort: the reset is already committed, so a failing mail server must not turn a succeeded
    // operation into an error. Doubles as a hijack tripwire — if the owner did not do this, the notice
    // is their signal.
    private async Task NotifyPasswordResetAsync(string email, CancellationToken ct)
    {
        const string subject = "Your password was reset";
        const string body = """
            <p>The password of your ATS account was just reset, and every signed-in session was
            ended.</p>
            <p>If you did this, no action is needed — sign in with your new password.</p>
            <p>If you did not, someone else may have access to your email — please contact your
            administrator immediately.</p>
            """;

        try
        {
            await _emailSender.SendAsync(email, subject, body, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the password-reset notification email");
        }
    }
}
