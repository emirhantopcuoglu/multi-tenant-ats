using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateProfileService : ICandidateProfileService
{
    private readonly CandidateAccountsDbContext _db;

    public CandidateProfileService(CandidateAccountsDbContext db)
    {
        _db = db;
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

    private static CandidateProfileDto ToDto(CandidateAccount account) =>
        new(account.Id, account.Email, account.FirstName, account.LastName,
            account.PhoneNumber, account.Country, account.City, account.BirthDate);

    private static string? NullIfWhiteSpace(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
