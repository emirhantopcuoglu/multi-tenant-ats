using System.Net;
using System.Security.Cryptography;
using System.Text;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ats.Modules.Tenants.Infrastructure;

public sealed class InvitationService : IInvitationService
{
    private readonly TenantsDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ICurrentUser _currentUser;
    private readonly InvitationOptions _options;

    public InvitationService(
        TenantsDbContext db,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ICurrentUser currentUser,
        IOptions<InvitationOptions> options)
    {
        _db = db;
        _userManager = userManager;
        _emailSender = emailSender;
        _emailText = emailText;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<Result> InviteAsync(string email, string role, CancellationToken ct = default)
    {
        if (!Roles.All.Contains(role))
            return Result.Failure(InvitationErrors.InvalidRole);

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            return Result.Failure(InvitationErrors.EmailInUse);

        var rawToken = GenerateToken();
        var invitation = Invitation.Create(email, role, Hash(rawToken), _options.ValidDays);

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync(ct);

        var link = $"{_options.AcceptBaseUrl}?token={rawToken}";

        // The invitee has no account yet, so there is no stored preference to read. The inviter's
        // language is the best evidence available — colleagues at one company overwhelmingly share
        // one — and the invitee can switch the moment they land on the accept page.
        var language = await ResolveInviterLanguageAsync(ct);

        // Role is one of the values checked against Roles.All above, so it cannot carry markup, but
        // it is encoded anyway rather than left as the one interpolation that skips the rule.
        var body = _emailText.Get(
            EmailTextKeys.Company.InvitationBody,
            language,
            WebUtility.HtmlEncode(role),
            link,
            _options.ValidDays);

        await _emailSender.SendAsync(
            email, _emailText.Get(EmailTextKeys.Company.InvitationSubject, language), body, ct);
        return Result.Success();
    }

    public async Task<Result> AcceptAsync(
        string token, string password, string firstName, string lastName, string preferredLanguage,
        CancellationToken ct = default)
    {
        var hash = Hash(token);
        var invitation = await _db.Invitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);

        if (invitation is null || !invitation.IsValid)
            return Result.Failure(InvitationErrors.InvalidToken);

        var user = new ApplicationUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            FirstName = firstName,
            LastName = lastName,
            TenantId = invitation.TenantId,
            CreatedAtUtc = DateTime.UtcNow,
            PreferredLanguage = SupportedLanguages.Normalize(preferredLanguage),
            // Already proven, by construction: reaching this line required clicking a link that was
            // mailed to this exact address. Asking an invited colleague to confirm a second time would
            // be demanding the same evidence twice — and would gate them out of the workspace they were
            // just invited to. Self-registration is the only path that needs the separate proof.
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return Result.Failure(InvitationErrors.CreationFailed(
                string.Join("; ", result.Errors.Select(e => e.Description))));

        await _userManager.AddToRoleAsync(user, invitation.Role);

        invitation.MarkAccepted();
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }

    // Falls back to the default rather than failing: an invitation must still go out if the inviter's
    // row cannot be read, and the worst outcome is an email in the wrong one of two languages.
    private async Task<string> ResolveInviterLanguageAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } inviterId)
            return SupportedLanguages.Default;

        var language = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == inviterId)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(ct);

        return SupportedLanguages.Normalize(language);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}
