using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Logins write the failure counter to the account row, so the candidate's own save can lose the
// xmin check to a change it doesn't care about. Rerunning the whole read, check and save on the
// fresh row fixes that, and a real race (a delete in between) is decided again on what is there now.
internal static class ConflictRetry
{
    private const int MaxAttempts = 5;

    public static async Task<T> RunAsync<T>(DbContext db, Func<Task<T>> readCheckAndSave)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await readCheckAndSave();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                db.ChangeTracker.Clear();
            }
        }
    }
}
