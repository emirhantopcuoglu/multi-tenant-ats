using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// Alias because "Application" is also a namespace here.
using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Infrastructure;

// Erases a deleted candidate's data in every company they applied to. Runs from the account deletion
// job with no tenant, so the query filters are bypassed.
public sealed class ApplicationCandidateDataEraser : ICandidateDataEraser
{
    private readonly ApplicationsDbContext _db;
    private readonly IPublisher _publisher;
    private readonly IActivityLogRepository _activityLog;
    private readonly ApplicationPersonalDataEraser _personalData;
    private readonly ILogger<ApplicationCandidateDataEraser> _logger;

    public ApplicationCandidateDataEraser(
        ApplicationsDbContext db,
        IPublisher publisher,
        IActivityLogRepository activityLog,
        ApplicationPersonalDataEraser personalData,
        ILogger<ApplicationCandidateDataEraser> logger)
    {
        _db = db;
        _publisher = publisher;
        _activityLog = activityLog;
        _personalData = personalData;
        _logger = logger;
    }

    public async Task EraseAsync(Guid candidateAccountId, CancellationToken cancellationToken = default)
    {
        // The job calls this for every due account in one scope. A failed save leaves its changes
        // tracked, and the next account's save would retry them.
        _db.ChangeTracker.Clear();

        // Soft-deleted applications too: they still hold the CV and the cover letter.
        var applications = await _db.Applications
            .IgnoreQueryFilters()
            .Where(a => a.CandidateAccountId == candidateAccountId)
            .ToListAsync(cancellationToken);
        if (applications.Count == 0)
            return;

        var withdrawn = await WithdrawActiveAsync(applications, cancellationToken);
        var candidateIds = applications.Select(a => a.CandidateId).Distinct().ToList();
        var candidates = await _db.Candidates
            .IgnoreQueryFilters()
            .Where(c => candidateIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        await _personalData.EraseAsync(applications, candidates, cancellationToken);
        await LogWithdrawalsAsync(withdrawn, cancellationToken);
        await ScrubRequestDetailsAsync(candidateAccountId, cancellationToken);
    }

    // The same as the candidate withdrawing: the board drops it and its upcoming interviews are
    // cancelled. Published before the save so the outbox message commits with the change.
    private async Task<List<ApplicationEntity>> WithdrawActiveAsync(
        List<ApplicationEntity> applications, CancellationToken cancellationToken)
    {
        var active = applications.Where(a => a.Status == ApplicationStatus.Active).ToList();
        foreach (var application in active)
        {
            application.Withdraw();
            await _publisher.Publish(
                new ApplicationWithdrawnEvent(application.Id, application.TenantId), cancellationToken);
        }

        return active;
    }

    private async Task LogWithdrawalsAsync(List<ApplicationEntity> withdrawn, CancellationToken cancellationToken)
    {
        foreach (var application in withdrawn)
        {
            await _activityLog.TryAddAsync(
                ApplicationActivity.Withdrawn(application.Id), application.TenantId, _logger, cancellationToken);
        }
    }

    // The candidate's email, IP and browser on the requests they made themselves.
    private async Task ScrubRequestDetailsAsync(Guid candidateAccountId, CancellationToken cancellationToken)
    {
        await _db.Set<AuditLogEntry>()
            .Where(e => e.ActorUserId == candidateAccountId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(e => e.ActorEmail, (string?)null)
                    .SetProperty(e => e.IpAddress, (string?)null)
                    .SetProperty(e => e.UserAgent, (string?)null),
                cancellationToken);
    }
}
