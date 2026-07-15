using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateProfileService : ICandidateProfileService
{
    private readonly CandidateAccountsDbContext _db;
    private readonly ICandidatePasswordHasher _passwordHasher;
    private readonly ICandidateTokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<CandidateProfileService> _logger;

    public CandidateProfileService(
        CandidateAccountsDbContext db,
        ICandidatePasswordHasher passwordHasher,
        ICandidateTokenService tokenService,
        IEmailSender emailSender,
        ILogger<CandidateProfileService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<Result<CandidateProfileDto>> GetAsync(Guid candidateAccountId)
    {
        var account = await _db.CandidateAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateAccountId);

        return account is null
            ? Result.Failure<CandidateProfileDto>(CandidateProfileErrors.NotFound)
            : Result.Success(ToDto(account));
    }

    public async Task<Result<CandidateProfileDto>> UpdateAsync(
        Guid candidateAccountId, UpdateCandidateProfileCommand command)
    {
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure<CandidateProfileDto>(CandidateProfileErrors.NotFound);

        var country = NullIfWhiteSpace(command.Country);
        var city = NullIfWhiteSpace(command.City);

        // Catalogue membership is checked here, not in the domain: the entity owns self-contained
        // invariants, the boundary owns "is this value in the supported list" (same split as Jobs).
        if (country is not null &&
            (!SupportedCountries.CitiesByCountry.TryGetValue(country, out var cities) ||
             city is null || !cities.Contains(city)))
        {
            return Result.Failure<CandidateProfileDto>(CandidateProfileErrors.UnsupportedLocation);
        }

        try
        {
            account.UpdateProfile(
                command.FirstName, command.LastName, command.PhoneNumber, country, city, command.BirthDate);
        }
        catch (ArgumentException invariantViolation)
        {
            // Domain guards throw; over HTTP that must read as a 400 with the failed rule, not a 500.
            return Result.Failure<CandidateProfileDto>(
                CandidateProfileErrors.InvalidData(invariantViolation.Message));
        }

        await _db.SaveChangesAsync();
        return Result.Success(ToDto(account));
    }

    public async Task<Result<CandidatePasswordChangeResult>> ChangePasswordAsync(
        Guid candidateAccountId, ChangeCandidatePasswordCommand command)
    {
        if (!CandidatePasswordPolicy.IsAcceptable(command.NewPassword))
            return Result.Failure<CandidatePasswordChangeResult>(CandidateProfileErrors.PasswordTooShort);

        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure<CandidatePasswordChangeResult>(CandidateProfileErrors.NotFound);

        // A valid token is not enough to change the password: a stolen session must not be able to
        // lock the real owner out, so the current password re-proves ownership here.
        if (!_passwordHasher.Verify(account.PasswordHash, command.CurrentPassword))
            return Result.Failure<CandidatePasswordChangeResult>(CandidateProfileErrors.InvalidCurrentPassword);

        account.ChangePassword(_passwordHasher.Hash(command.NewPassword));
        await _db.SaveChangesAsync();

        // Structured security-event log: the id is enough to investigate, and neither password nor
        // hash may ever appear in a log line.
        _logger.LogInformation(
            "Password changed for candidate account {CandidateAccountId}", candidateAccountId);

        await NotifyPasswordChangedAsync(account.Email);

        // The rotation above just invalidated the token this request arrived with; hand back a fresh
        // one so the candidate's own session survives their password change.
        var accessToken = _tokenService.GenerateAccessToken(account.Id, account.Email, account.SecurityStamp);
        return Result.Success(new CandidatePasswordChangeResult(accessToken));
    }

    // Best-effort by design: the password change is already committed, so a failing mail server must
    // not turn a succeeded operation into an error response. The failure is logged, not swallowed.
    private async Task NotifyPasswordChangedAsync(string email)
    {
        const string subject = "Your password was changed";
        const string body = """
            <p>The password of your candidate account was just changed.</p>
            <p>If you made this change, no action is needed.</p>
            <p>If you did not, someone else may have access to your account — please contact us immediately.</p>
            """;

        try
        {
            await _emailSender.SendAsync(email, subject, body);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the password-changed notification email");
        }
    }

    private static CandidateProfileDto ToDto(CandidateAccount account) =>
        new(account.Id, account.Email, account.FirstName, account.LastName,
            account.PhoneNumber, account.Country, account.City, account.BirthDate);

    private static string? NullIfWhiteSpace(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
