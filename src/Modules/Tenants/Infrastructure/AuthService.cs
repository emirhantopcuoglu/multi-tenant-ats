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

    // Returns no tokens, unlike the candidate side. A company user who can sign in can invite
    // colleagues, publish jobs and start receiving applications — there is no single consequential
    // action to gate the way "apply" gates a candidate, so the session itself waits for the address to
    // be proven. Which makes the resend endpoint below anonymous by necessity: someone who cannot sign
    // in is exactly who needs it.
    public async Task<Result> RegisterAsync(
        string companyName, string slug, string email, string password, string firstName, string lastName,
        string preferredLanguage)
    {
        // Normalize once, then validate and check uniqueness against that exact value. The slug is
        // stored lower-cased, so comparing the raw input would let "Acme" pass the uniqueness check
        // against a stored "acme" and then collide on insert.
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

        // The tenant is staged, not saved: it used to be committed here, before the user existed, so a
        // rejected password (or any Identity failure below) left an orphan tenant behind holding the
        // slug — permanently unregisterable by anyone, including whoever just failed. Now both rows land
        // in the CreateAsync call's save, or neither does.
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
            // Normalized at this boundary, not in the entity: an unrecognised language becomes English
            // rather than failing a registration over a value the caller may not control.
            PreferredLanguage = SupportedLanguages.Normalize(preferredLanguage)
        };

        // Identity validates before it saves, so a rejected password leaves the staged tenant unsaved.
        // The context is scoped to this request and nothing else writes to it afterwards, so the staged
        // entity dies with the request — no explicit cleanup, and the slug stays free for a retry.
        var identityResult = await _userManager.CreateAsync(user, password);
        if (!identityResult.Succeeded)
            return Result.Failure(
                AuthErrors.RegistrationFailed(string.Join("; ", identityResult.Errors.Select(e => e.Description))));

        // The user who registers a tenant is its founder and therefore its administrator.
        await _userManager.AddToRoleAsync(user, Roles.Admin);

        await SendEmailConfirmationLinkAsync(user);

        return Result.Success();
    }

    public async Task<Result<AuthResult>> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, password))
            return Result.Failure<AuthResult>(AuthErrors.InvalidCredentials);

        // A deactivated user answers exactly like a wrong password. They are not owed an explanation
        // of their own employment status by a login form, and a distinct message would let anyone with
        // a leaked password list learn which accounts still exist.
        if (!user.IsActive)
            return Result.Failure<AuthResult>(AuthErrors.InvalidCredentials);

        // Checked after the password, so this only ever answers someone who has proved the account is
        // theirs — see AuthErrors.EmailNotConfirmed for why a distinct code is safe here but not for
        // deactivation. Invited users are confirmed at creation, so this only stops self-registrations.
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

        // Deactivation revokes outstanding refresh tokens, so this branch should already be
        // unreachable for a deactivated user. It stays as the durable guard: that revocation is a
        // one-time sweep, while this runs on every redemption, so any token that escapes it — a race
        // with a concurrent login, a row written by an older build — still stops here.
        if (!user.IsActive)
            return Result.Failure<AuthResult>(AuthErrors.InvalidRefreshToken);

        // Registration no longer issues tokens, so an unconfirmed user should hold no refresh token to
        // redeem and this branch should be unreachable. It stays for the same reason as the IsActive
        // guard above: that is an argument about the current code, not an invariant the database
        // enforces, and a row written by an older build would otherwise sail straight through.
        // InvalidRefreshToken, not EmailNotConfirmed — this endpoint is anonymous, so unlike login
        // nobody here has proved anything.
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

        // Already confirmed is a success, not an error: this is a link in an email, and a second click
        // — a duplicate tab, a mail client prefetching URLs — must not tell someone their working
        // account is broken. Contrast the candidate side, where the row is explicitly consumed and a
        // replay fails; Identity's token is stateless, so "already done" is all we can distinguish.
        if (user.EmailConfirmed)
            return Result.Success();

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return Result.Failure(AuthErrors.InvalidEmailConfirmationToken);

        _logger.LogInformation("Email confirmed for user {UserId}", user.Id);

        return Result.Success();
    }

    // Anonymous by necessity: the person who needs this cannot sign in, which is the whole problem.
    // That forces the same anti-enumeration silence as RequestPasswordResetAsync — always success, so
    // the endpoint cannot be used to discover which addresses work here. It also stays quiet for an
    // already-confirmed account: answering differently would reveal that the address is registered.
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

        await NotifyPasswordResetAsync(user.Email!, user.PreferredLanguage, ct);

        return Result.Success();
    }

    public async Task<Result> SetPreferredLanguageAsync(
        Guid userId, string language, CancellationToken ct = default)
    {
        // Rejected rather than normalized: this endpoint exists only to set the language, so a value
        // outside the catalogue is a client bug, and quietly storing English would hide it.
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
            // Active first, then by name: a deactivated colleague stays visible (so an Admin can
            // reactivate them) without cluttering the top of the interviewer picker.
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

    // Best-effort, like the password reset link: registration is already committed by the time this
    // runs, so a failing mail server must not report a failed registration for an account that exists.
    // The founder can resend from the login screen. Logged as an error so an SMTP outage is visible to
    // an operator rather than only to a confused user.
    private async Task SendEmailConfirmationLinkAsync(
        ApplicationUser user, CancellationToken ct = default)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        // userId rather than the email in the URL, for the same reason as the reset link: the address
        // would otherwise land in browser history, referrer headers and any proxy log en route.
        var link = $"{_emailConfirmationOptions.ConfirmBaseUrl}" +
                   $"?userId={Uri.EscapeDataString(user.Id.ToString())}" +
                   $"&token={Uri.EscapeDataString(token)}";

        var language = user.PreferredLanguage;

        // The name came from a registration form, so it is untrusted inside an HTML body and is
        // encoded before it reaches the template.
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
        // The user id, not the email, goes in the URL: the address would otherwise end up in browser
        // history, referrer headers and any proxy log the link passes through. Both values are escaped
        // because Identity's token is base64-ish and contains characters that are unsafe unencoded.
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

    // Best-effort: the reset is already committed, so a failing mail server must not turn a succeeded
    // operation into an error. Doubles as a hijack tripwire — if the owner did not do this, the notice
    // is their signal.
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
