using System.Net;
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
    private readonly EmailConfirmationOptions _emailConfirmationOptions;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        TenantsDbContext db,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        IOptions<PasswordResetOptions> passwordResetOptions,
        IOptions<EmailConfirmationOptions> emailConfirmationOptions,
        ICurrentTenant currentTenant,
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _db = db;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _passwordResetOptions = passwordResetOptions.Value;
        _emailConfirmationOptions = emailConfirmationOptions.Value;
        _currentTenant = currentTenant;
        _emailSender = emailSender;
        _emailText = emailText;
        _logger = logger;
    }

    // Returns no tokens: company login waits for a confirmed email, unlike the candidate side.
    public async Task<Result> RegisterAsync(
        string companyName, string slug, string email, string password, string firstName, string lastName,
        string preferredLanguage)
    {
        // Normalize first so the uniqueness check matches the stored lower-case slug.
        var normalizedSlug = (slug ?? string.Empty).Trim().ToLowerInvariant();

        var slugValidation = SlugPolicy.Validate(normalizedSlug);
        if (slugValidation.IsFailure)
            return Result.Failure(slugValidation.Error);

        var slugTaken = await _db.Tenants.AnyAsync(t => t.Slug == normalizedSlug);
        if (slugTaken)
            return Result.Failure(AuthErrors.RegistrationFailed($"Slug '{normalizedSlug}' is already taken."));

        var emailTaken = await _userManager.FindByEmailAsync(email) is not null;
        if (emailTaken)
            return Result.Failure(AuthErrors.RegistrationFailed($"Email '{email}' is already registered."));

        // Staged, not saved, so a failed user creation doesn't leave a tenant holding the slug.
        var tenant = Tenant.Create(companyName, normalizedSlug);
        _db.Tenants.Add(tenant);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            TenantId = tenant.Id,
            CreatedAtUtc = DateTime.UtcNow,
            PreferredLanguage = SupportedLanguages.Normalize(preferredLanguage)
        };

        // A rejected password means nothing is saved; the staged tenant is dropped with the request.
        var identityResult = await _userManager.CreateAsync(user, password);
        if (!identityResult.Succeeded)
            return Result.Failure(
                AuthErrors.RegistrationFailed(string.Join("; ", identityResult.Errors.Select(e => e.Description))));

        await _userManager.AddToRoleAsync(user, Roles.Admin);

        await SendEmailConfirmationLinkAsync(user);

        return Result.Success();
    }

    public async Task<Result<AuthResult>> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Result.Failure<AuthResult>(AuthErrors.InvalidCredentials);

        // Checked before the password, otherwise guesses during a lockout would keep extending it.
        // Same error as a wrong password, so it doesn't reveal that the account exists.
        if (await _userManager.IsLockedOutAsync(user))
            return Result.Failure<AuthResult>(AuthErrors.InvalidCredentials);

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            await _userManager.AccessFailedAsync(user);
            return Result.Failure<AuthResult>(AuthErrors.InvalidCredentials);
        }

        if (user.AccessFailedCount > 0)
            await _userManager.ResetAccessFailedCountAsync(user);

        // Same error as a wrong password, so it doesn't reveal which accounts still exist.
        if (!user.IsActive)
            return Result.Failure<AuthResult>(AuthErrors.InvalidCredentials);

        // Safe to give a distinct error here: the caller has already proved the password.
        if (!user.EmailConfirmed)
            return Result.Failure<AuthResult>(AuthErrors.EmailNotConfirmed);

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

        // Deactivation revokes refresh tokens, but this check still catches any that slip through.
        if (!user.IsActive)
            return Result.Failure<AuthResult>(AuthErrors.InvalidRefreshToken);

        // Registration issues no tokens, so this is a safety net. InvalidRefreshToken because the
        // caller hasn't proved anything here.
        if (!user.EmailConfirmed)
            return Result.Failure<AuthResult>(AuthErrors.InvalidRefreshToken);

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

    public async Task<Result> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(AuthErrors.InvalidEmailConfirmationToken);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure(AuthErrors.InvalidEmailConfirmationToken);

        // A second click on the link is a success, not an error.
        if (user.EmailConfirmed)
            return Result.Success();

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return Result.Failure(AuthErrors.InvalidEmailConfirmationToken);

        _logger.LogInformation("Email confirmed for user {UserId}", user.Id);

        return Result.Success();
    }

    // Always succeeds, so it can't be used to find registered or confirmed addresses.
    public async Task<Result> ResendEmailConfirmationAsync(string email, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email ?? string.Empty);

        if (user is null || user.EmailConfirmed)
        {
            _logger.LogInformation(
                "Email confirmation resend requested for an unregistered or already-confirmed address");
            return Result.Success();
        }

        await SendEmailConfirmationLinkAsync(user, ct);

        return Result.Success();
    }

    public async Task<Result> RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email ?? string.Empty);

        if (user is null)
        {
            // Same answer as for a registered email.
            _logger.LogInformation("Password reset requested for an unregistered email");
            return Result.Success();
        }

        // Identity's token embeds the security stamp, so it stops working once the password changes.
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        _logger.LogInformation("Password reset requested for user {UserId}", user.Id);

        // Best-effort: failing here would reveal that the email is registered.
        await SendPasswordResetLinkAsync(user.Email!, user.PreferredLanguage, user.Id, token, ct);

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
            // Only a bad token gets the vague error; a rejected password is something the user can fix.
            var isTokenFailure = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken));
            return isTokenFailure
                ? Result.Failure(AuthErrors.InvalidPasswordResetToken)
                : Result.Failure(AuthErrors.PasswordRejected(
                    string.Join("; ", result.Errors.Select(e => e.Description))));
        }

        // A password reset is how a locked-out user gets back in.
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);

        // Company tokens have no security stamp, so refresh tokens must be revoked explicitly.
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var refreshToken in activeTokens)
            refreshToken.Revoke();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset completed for user {UserId}; revoked {RevokedCount} refresh token(s)",
            user.Id, activeTokens.Count);

        await NotifyPasswordResetAsync(user.Email!, user.PreferredLanguage, ct);

        return Result.Success();
    }

    public async Task<Result> SetPreferredLanguageAsync(
        Guid userId, string language, CancellationToken ct = default)
    {
        // Rejected, not normalized: this endpoint only sets the language, so a bad value is a client bug.
        if (!SupportedLanguages.IsSupported(language))
            return Result.Failure(AuthErrors.UnsupportedLanguage);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure(AuthErrors.UserNotFound);

        user.PreferredLanguage = language;
        await _userManager.UpdateAsync(user);

        return Result.Success();
    }

    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure<CurrentUserDto>(AuthErrors.UserNotFound);

        var tenant = user.TenantId is { } tenantId
            ? await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId)
            : null;
        if (tenant is null)
            return Result.Failure<CurrentUserDto>(AuthErrors.UserNotFound);

        // One role per user.
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

        // One query instead of GetRolesAsync per user. ApplicationUser isn't ITenantScoped, so the
        // tenant filter is explicit. Active users first.
        var users = await (
            from user in _db.Users.AsNoTracking()
            where user.TenantId == tenantId
            join userRole in _db.UserRoles on user.Id equals userRole.UserId into userRoles
            from userRole in userRoles.DefaultIfEmpty()
            join role in _db.Roles on userRole.RoleId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            orderby user.DeactivatedAtUtc == null descending, user.FirstName, user.LastName
            select new TenantUserDto(
                user.Id, user.FirstName, user.LastName, user.Email!,
                role != null ? role.Name! : string.Empty,
                user.DeactivatedAtUtc == null))
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

    // Best-effort: the user can resend from the login screen.
    private async Task SendEmailConfirmationLinkAsync(
        ApplicationUser user, CancellationToken ct = default)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        // User id, not email, in the URL, so the address doesn't end up in logs or history.
        var link = $"{_emailConfirmationOptions.ConfirmBaseUrl}" +
                   $"?userId={Uri.EscapeDataString(user.Id.ToString())}" +
                   $"&token={Uri.EscapeDataString(token)}";

        var language = user.PreferredLanguage;

        // The name is user input going into HTML.
        var body = _emailText.Get(
            EmailTextKeys.Company.ConfirmEmailBody,
            language,
            WebUtility.HtmlEncode(user.FirstName),
            link,
            EmailConfirmationTokenProviderOptions.ValidHours);

        try
        {
            await _emailSender.SendAsync(
                user.Email!,
                _emailText.Get(EmailTextKeys.Company.ConfirmEmailSubject, language),
                body,
                ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the email confirmation link");
        }
    }

    private async Task SendPasswordResetLinkAsync(
        string email, string language, Guid userId, string token, CancellationToken ct)
    {
        // User id, not email, in the URL, so the address doesn't end up in logs or history.
        var link = $"{_passwordResetOptions.ResetBaseUrl}" +
                   $"?userId={Uri.EscapeDataString(userId.ToString())}" +
                   $"&token={Uri.EscapeDataString(token)}";

        var body = _emailText.Get(
            EmailTextKeys.Company.ResetPasswordBody, language, link, _passwordResetOptions.ValidMinutes);

        try
        {
            await _emailSender.SendAsync(
                email, _emailText.Get(EmailTextKeys.Company.ResetPasswordSubject, language), body, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the password reset email");
        }
    }

    // Best-effort: the reset is already saved.
    private async Task NotifyPasswordResetAsync(string email, string language, CancellationToken ct)
    {
        var subject = _emailText.Get(EmailTextKeys.Company.PasswordResetSubject, language);
        var body = _emailText.Get(EmailTextKeys.Company.PasswordResetBody, language);

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
