using System.Globalization;
using System.Net;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateAccountLifecycleService : ICandidateAccountLifecycleService
{
    // The culture comes from the account's language, so the month is spelled in it.
    private const string DeletionDateFormat = "d MMMM yyyy";

    private readonly CandidateAccountsDbContext _db;
    private readonly ICandidatePasswordHasher _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly ILogger<CandidateAccountLifecycleService> _logger;

    public CandidateAccountLifecycleService(
        CandidateAccountsDbContext db,
        ICandidatePasswordHasher passwordHasher,
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        ILogger<CandidateAccountLifecycleService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _emailText = emailText;
        _logger = logger;
    }

    public async Task<Result> FreezeAsync(Guid candidateAccountId)
    {
        var frozen = await ChangeStatusAsync(candidateAccountId, account => account.Freeze());
        if (frozen.IsSuccess)
            _logger.LogInformation("Candidate account {CandidateAccountId} frozen", candidateAccountId);

        return frozen;
    }

    public async Task<Result> ReactivateAsync(Guid candidateAccountId)
    {
        var reactivated = await ChangeStatusAsync(candidateAccountId, account => account.Reactivate());
        if (reactivated.IsSuccess)
            _logger.LogInformation("Candidate account {CandidateAccountId} reactivated", candidateAccountId);

        return reactivated;
    }

    public async Task<Result> CancelDeletionAsync(Guid candidateAccountId)
    {
        var cancelled = await ChangeStatusAsync(candidateAccountId, account => account.CancelDeletion());
        if (cancelled.IsSuccess)
            _logger.LogInformation("Deletion of candidate account {CandidateAccountId} cancelled", candidateAccountId);

        return cancelled;
    }

    private Task<Result> ChangeStatusAsync(Guid candidateAccountId, Action<CandidateAccount> transition) =>
        ConflictRetry.RunAsync(_db, async () =>
        {
            var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
            if (account is null)
                return Result.Failure(CandidateAccountLifecycleErrors.NotFound);

            try
            {
                transition(account);
            }
            catch (InvalidOperationException rejectedTransition)
            {
                return Result.Failure(CandidateAccountLifecycleErrors.InvalidState(rejectedTransition.Message));
            }

            await _db.SaveChangesAsync();
            return Result.Success();
        });

    // CandidateAccountDeletionJob erases the account once the grace period is over.
    public async Task<Result<DateTime>> RequestDeletionAsync(
        Guid candidateAccountId, RequestCandidateAccountDeletionCommand command)
    {
        var requested = await ConflictRetry.RunAsync(_db, async () =>
        {
            var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
            if (account is null)
                return Result.Failure<CandidateAccount>(CandidateAccountLifecycleErrors.NotFound);

            // A stolen session alone must not be able to delete the account.
            if (!_passwordHasher.Verify(account.PasswordHash, command.CurrentPassword))
                return Result.Failure<CandidateAccount>(CandidateAccountLifecycleErrors.InvalidCurrentPassword);

            try
            {
                account.RequestDeletion(DateTime.UtcNow);
            }
            catch (InvalidOperationException rejectedTransition)
            {
                return Result.Failure<CandidateAccount>(
                    CandidateAccountLifecycleErrors.InvalidState(rejectedTransition.Message));
            }

            await _db.SaveChangesAsync();
            return Result.Success(account);
        });

        if (requested.IsFailure)
            return Result.Failure<DateTime>(requested.Error);

        var account = requested.Value;
        var dueAtUtc = account.DeletionDueAtUtc!.Value;
        _logger.LogInformation(
            "Deletion of candidate account {CandidateAccountId} requested, due {DeletionDueAtUtc}", account.Id, dueAtUtc);

        await NotifyDeletionRequestedAsync(account, dueAtUtc);
        return Result.Success(dueAtUtc);
    }

    // Best-effort: the request is already saved.
    private async Task NotifyDeletionRequestedAsync(CandidateAccount account, DateTime dueAtUtc)
    {
        var language = account.PreferredLanguage;
        var dueDate = dueAtUtc.ToString(DeletionDateFormat, CultureInfo.GetCultureInfo(language));

        // The name is user input going into HTML.
        var body = _emailText.Get(
            EmailTextKeys.Candidate.DeletionRequestedBody, language, WebUtility.HtmlEncode(account.FirstName), dueDate);

        try
        {
            await _emailSender.SendAsync(
                account.Email, _emailText.Get(EmailTextKeys.Candidate.DeletionRequestedSubject, language), body);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception, "Failed to send the deletion request email of candidate account {CandidateAccountId}", account.Id);
        }
    }
}
