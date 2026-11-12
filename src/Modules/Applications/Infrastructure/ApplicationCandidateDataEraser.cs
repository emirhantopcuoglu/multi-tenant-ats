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
// job with no tenant, so the query filters are bypassed and the audit interceptor stays out: an audit
// row for the erasure would copy the old values back.
public sealed class ApplicationCandidateDataEraser : ICandidateDataEraser
{
    private static readonly string ApplicationResourceType = typeof(ApplicationEntity).Name;
    private static readonly string CandidateResourceType = typeof(Candidate).Name;

    private readonly ApplicationsDbContext _db;
    private readonly IPublisher _publisher;
    private readonly IActivityLogRepository _activityLog;
    private readonly ICvParseResultRepository _cvParseResults;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<ApplicationCandidateDataEraser> _logger;

    public ApplicationCandidateDataEraser(
        ApplicationsDbContext db,
        IPublisher publisher,
        IActivityLogRepository activityLog,
        ICvParseResultRepository cvParseResults,
        IFileStorage fileStorage,
        ILogger<ApplicationCandidateDataEraser> logger)
    {
        _db = db;
        _publisher = publisher;
        _activityLog = activityLog;
        _cvParseResults = cvParseResults;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    // Postgres first: once no row points to a CV, a failure later still leaves the file to the orphan
    // cleanup.
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
        var cvFiles = await EraseRowsAsync(applications, cancellationToken);
        await LogWithdrawalsAsync(withdrawn, cancellationToken);
        await ScrubAuditLogAsync(candidateAccountId, applications, cancellationToken);
        await EraseDocumentsAsync(applications, cancellationToken);
        await TryDeleteCvFilesAsync(cvFiles, cancellationToken);
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

    private async Task<List<(Guid ApplicationId, string Key)>> EraseRowsAsync(
        List<ApplicationEntity> applications, CancellationToken cancellationToken)
    {
        var cvFiles = applications
            .Select(a => (ApplicationId: a.Id, Key: a.ErasePersonalData()))
            .Where(file => file.Key is not null)
            .Select(file => (file.ApplicationId, file.Key!))
            .ToList();

        var candidateIds = applications.Select(a => a.CandidateId).Distinct().ToList();
        var candidates = await _db.Candidates
            .IgnoreQueryFilters()
            .Where(c => candidateIds.Contains(c.Id))
            .ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
            candidate.Erase();

        await _db.SaveChangesAsync(cancellationToken);
        return cvFiles;
    }

    private async Task LogWithdrawalsAsync(List<ApplicationEntity> withdrawn, CancellationToken cancellationToken)
    {
        foreach (var application in withdrawn)
        {
            await _activityLog.TryAddAsync(
                ApplicationActivity.Withdrawn(application.Id), application.TenantId, _logger, cancellationToken);
        }
    }

    // The rows keep who did what and when. The old values go, and so do the candidate's email, IP and
    // browser on their own requests.
    private async Task ScrubAuditLogAsync(
        Guid candidateAccountId, List<ApplicationEntity> applications, CancellationToken cancellationToken)
    {
        var applicationIds = applications.Select(a => a.Id.ToString()).ToList();
        var candidateIds = applications.Select(a => a.CandidateId.ToString()).Distinct().ToList();
        var auditLog = _db.Set<AuditLogEntry>();

        await auditLog
            .Where(e => (e.ResourceType == ApplicationResourceType && applicationIds.Contains(e.ResourceId))
                        || (e.ResourceType == CandidateResourceType && candidateIds.Contains(e.ResourceId)))
            .ExecuteUpdateAsync(
                set => set.SetProperty(e => e.Before, (string?)null).SetProperty(e => e.After, (string?)null),
                cancellationToken);

        await auditLog
            .Where(e => e.ActorUserId == candidateAccountId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(e => e.ActorEmail, (string?)null)
                    .SetProperty(e => e.IpAddress, (string?)null)
                    .SetProperty(e => e.UserAgent, (string?)null),
                cancellationToken);
    }

    // Grouped by company: every Mongo query carries the tenant.
    private async Task EraseDocumentsAsync(List<ApplicationEntity> applications, CancellationToken cancellationToken)
    {
        foreach (var company in applications.GroupBy(a => a.TenantId))
        {
            var applicationIds = company.Select(a => a.Id).ToList();
            await _activityLog.ErasePersonalDataAsync(company.Key, applicationIds, cancellationToken);
            await _cvParseResults.DeleteAsync(company.Key, applicationIds, cancellationToken);
        }
    }

    // A storage outage must not fail the erasure. No row points to these files now, so the orphan file
    // cleanup removes them later. The key holds the file name, so only the application id is logged.
    private async Task TryDeleteCvFilesAsync(
        List<(Guid ApplicationId, string Key)> cvFiles, CancellationToken cancellationToken)
    {
        foreach (var (applicationId, key) in cvFiles)
        {
            try
            {
                await _fileStorage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Failed to delete the CV of erased application {ApplicationId}", applicationId);
            }
        }
    }
}
