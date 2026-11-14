using Ats.Modules.Applications.Domain;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Alias because "Application" is also a namespace here.
using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Infrastructure;

// Erases the personal data on applications closed longer ago than the retention period. Scheduled by
// Hangfire. Every application is created with a CV key and erasing clears it, so a row with a key still
// holds data.
public sealed class ClosedApplicationRetentionJob
{
    private const int MinimumRetentionMonths = 1;

    private readonly ApplicationsDbContext _db;
    private readonly IReadOnlyList<IApplicationDataEraser> _moduleErasers;
    private readonly ApplicationPersonalDataEraser _personalData;
    private readonly RetentionOptions _retention;
    private readonly ILogger<ClosedApplicationRetentionJob> _logger;

    public ClosedApplicationRetentionJob(
        ApplicationsDbContext db,
        IEnumerable<IApplicationDataEraser> moduleErasers,
        ApplicationPersonalDataEraser personalData,
        IOptions<RetentionOptions> retention,
        ILogger<ClosedApplicationRetentionJob> logger)
    {
        _db = db;
        _moduleErasers = moduleErasers.ToList();
        _personalData = personalData;
        _retention = retention.Value;
        _logger = logger;
    }

    // One application failing doesn't stop the others; the next run tries it again.
    public async Task EraseDueApplicationsAsync(CancellationToken ct = default)
    {
        // A zero here would erase every closed application at once.
        if (_retention.ClosedApplicationMonths < MinimumRetentionMonths)
            throw new InvalidOperationException(
                $"{RetentionOptions.SectionName}:{nameof(RetentionOptions.ClosedApplicationMonths)} must be at least {MinimumRetentionMonths}.");

        var cutoffUtc = DateTime.UtcNow.AddMonths(-_retention.ClosedApplicationMonths);

        // Read once, so an application that keeps failing can't hold up the rest.
        var dueIds = await _db.Applications
            .IgnoreQueryFilters()
            .Where(a => a.ClosedAtUtc < cutoffUtc && a.CvFileKey != null)
            .OrderBy(a => a.ClosedAtUtc)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var failedCount = 0;
        foreach (var applicationId in dueIds)
        {
            try
            {
                await EraseAsync(applicationId, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failedCount++;
                _logger.LogError(exception, "Failed to erase closed application {ApplicationId}", applicationId);
            }
            finally
            {
                // A failed save leaves its changes tracked, and the next application's save would retry them.
                _db.ChangeTracker.Clear();
            }
        }

        _logger.LogInformation(
            "Closed application retention erased {ErasedCount} of {DueCount} application(s)",
            dueIds.Count - failedCount, dueIds.Count);

        // Fails the run so it shows on the dashboard.
        if (failedCount > 0)
            throw new InvalidOperationException($"{failedCount} closed application(s) could not be erased.");
    }

    // The other modules go first: once the row is erased, this job never picks it up again.
    private async Task EraseAsync(Guid applicationId, CancellationToken ct)
    {
        foreach (var eraser in _moduleErasers)
            await eraser.EraseAsync([applicationId], ct);

        var application = await _db.Applications.IgnoreQueryFilters().SingleAsync(a => a.Id == applicationId, ct);
        await _personalData.EraseAsync([application], await CandidatesToEraseAsync(application, ct), ct);
    }

    // The contact details go with the candidate's last application in that company that still holds data.
    private async Task<List<Candidate>> CandidatesToEraseAsync(ApplicationEntity application, CancellationToken ct)
    {
        var stillNeeded = await _db.Applications
            .IgnoreQueryFilters()
            .AnyAsync(a => a.CandidateId == application.CandidateId && a.Id != application.Id && a.CvFileKey != null, ct);
        if (stillNeeded)
            return [];

        return await _db.Candidates
            .IgnoreQueryFilters()
            .Where(c => c.Id == application.CandidateId)
            .ToListAsync(ct);
    }
}
