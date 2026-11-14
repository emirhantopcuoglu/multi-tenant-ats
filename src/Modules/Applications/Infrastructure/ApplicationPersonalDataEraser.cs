using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// Alias because "Application" is also a namespace here.
using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Infrastructure;

// Erases the personal data on closed applications: the rows, the old audit values, the Mongo documents
// and the CV files. Used by the account erasure and the retention job, which both run with no tenant, so
// the audit interceptor writes nothing: an audit row for the erasure would copy the old values back.
public sealed class ApplicationPersonalDataEraser
{
    private static readonly string ApplicationResourceType = typeof(ApplicationEntity).Name;
    private static readonly string CandidateResourceType = typeof(Candidate).Name;

    private readonly ApplicationsDbContext _db;
    private readonly IActivityLogRepository _activityLog;
    private readonly ICvParseResultRepository _cvParseResults;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<ApplicationPersonalDataEraser> _logger;

    public ApplicationPersonalDataEraser(
        ApplicationsDbContext db,
        IActivityLogRepository activityLog,
        ICvParseResultRepository cvParseResults,
        IFileStorage fileStorage,
        ILogger<ApplicationPersonalDataEraser> logger)
    {
        _db = db;
        _activityLog = activityLog;
        _cvParseResults = cvParseResults;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    // The rows come tracked by the same context. The steps a later run can repeat go before the save: once
    // the CV key is cleared, the retention job no longer picks the row up. The files go last, when no row
    // points to them, so the orphan file cleanup covers a failed delete.
    public async Task EraseAsync(
        IReadOnlyCollection<ApplicationEntity> applications,
        IReadOnlyCollection<Candidate> candidates,
        CancellationToken cancellationToken)
    {
        await ScrubAuditLogAsync(applications, candidates, cancellationToken);
        await EraseDocumentsAsync(applications, cancellationToken);

        var cvFiles = applications
            .Select(a => (ApplicationId: a.Id, Key: a.ErasePersonalData()))
            .Where(file => file.Key is not null)
            .Select(file => (file.ApplicationId, file.Key!))
            .ToList();
        foreach (var candidate in candidates)
            candidate.Erase();
        await _db.SaveChangesAsync(cancellationToken);

        await TryDeleteCvFilesAsync(cvFiles, cancellationToken);
    }

    // The rows keep who did what and when; the old values go.
    private async Task ScrubAuditLogAsync(
        IReadOnlyCollection<ApplicationEntity> applications,
        IReadOnlyCollection<Candidate> candidates,
        CancellationToken cancellationToken)
    {
        var applicationIds = applications.Select(a => a.Id.ToString()).ToList();
        var candidateIds = candidates.Select(c => c.Id.ToString()).ToList();

        await _db.Set<AuditLogEntry>()
            .Where(e => (e.ResourceType == ApplicationResourceType && applicationIds.Contains(e.ResourceId))
                        || (e.ResourceType == CandidateResourceType && candidateIds.Contains(e.ResourceId)))
            .ExecuteUpdateAsync(
                set => set.SetProperty(e => e.Before, (string?)null).SetProperty(e => e.After, (string?)null),
                cancellationToken);
    }

    // Grouped by company: every Mongo query carries the tenant.
    private async Task EraseDocumentsAsync(
        IReadOnlyCollection<ApplicationEntity> applications, CancellationToken cancellationToken)
    {
        foreach (var company in applications.GroupBy(a => a.TenantId))
        {
            var applicationIds = company.Select(a => a.Id).ToList();
            await _activityLog.ErasePersonalDataAsync(company.Key, applicationIds, cancellationToken);
            await _cvParseResults.DeleteAsync(company.Key, applicationIds, cancellationToken);
        }
    }

    // A storage outage must not fail the erasure. The key holds the file name, so only the application id
    // is logged.
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
