using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Notifications.Infrastructure;

// Deletes a deleted candidate's own notifications and every notification about their applications; the
// company ones name the candidate. Applications past their retention period lose theirs the same way.
// A notification is not a record anyone needs to keep.
public sealed class NotificationCandidateDataEraser : ICandidateDataEraser, IApplicationDataEraser
{
    private readonly NotificationsDbContext _db;
    private readonly IApplicationDirectory _applications;

    public NotificationCandidateDataEraser(NotificationsDbContext db, IApplicationDirectory applications)
    {
        _db = db;
        _applications = applications;
    }

    public async Task EraseAsync(Guid candidateAccountId, CancellationToken cancellationToken = default)
    {
        await _db.Notifications
            .Where(n => n.RecipientType == NotificationRecipientType.Candidate && n.RecipientId == candidateAccountId)
            .ExecuteDeleteAsync(cancellationToken);

        var applicationIds = await _applications.GetApplicationIdsForCandidateAccountAsync(
            candidateAccountId, cancellationToken);
        await EraseAsync(applicationIds, cancellationToken);
    }

    public async Task EraseAsync(IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        if (applicationIds.Count == 0)
            return;

        // EF can't query inside the jsonb payload. The interpolated ids are sent as a parameter, not
        // pasted into the SQL.
        var ids = applicationIds.Select(id => id.ToString()).ToArray();
        await _db.Database.ExecuteSqlAsync(
            $"""DELETE FROM notifications."Notifications" WHERE "Payload" ->> 'applicationId' = ANY({ids})""",
            cancellationToken);
    }
}
