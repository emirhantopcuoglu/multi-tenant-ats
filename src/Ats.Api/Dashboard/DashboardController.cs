using Asp.Versioning;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Interviews;
using Ats.Shared.Contracts.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Api.Dashboard;

// Read-only tenant overview for the dashboard home. This is an API-composition concern, not a domain
// one: the four numbers live in three different modules, and the API root is the only layer allowed to
// know all of them. It reaches each module through its cross-module read port (IJobDirectory,
// IApplicationDirectory, IInterviewDirectory) rather than touching their schemas, so module boundaries
// stay intact. Authenticated access is enough — every tenant member may see the overview; tenant
// isolation is automatic via each port's global query filter.
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard")]
[ApiVersion("1.0")]
public sealed class DashboardController : ControllerBase
{
    // "This week" for the new-applications stat is defined as a rolling 7-day window. A fixed-length
    // window avoids week-boundary and timezone ambiguity and keeps the count trivially explainable.
    private static readonly TimeSpan NewApplicationsWindow = TimeSpan.FromDays(7);

    private readonly IJobDirectory _jobs;
    private readonly IApplicationDirectory _applications;
    private readonly IInterviewDirectory _interviews;

    public DashboardController(
        IJobDirectory jobs, IApplicationDirectory applications, IInterviewDirectory interviews)
    {
        _jobs = jobs;
        _applications = applications;
        _interviews = interviews;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var openJobs = await _jobs.CountOpenJobsAsync(cancellationToken);
        var newApplications = await _applications.CountApplicationsSinceAsync(
            now - NewApplicationsWindow, cancellationToken);
        var upcomingInterviews = await _interviews.CountUpcomingInterviewsAsync(now, cancellationToken);
        var activeCandidates = await _applications.CountActiveCandidatesAsync(cancellationToken);

        return Ok(new DashboardStatsDto(
            openJobs, newApplications, upcomingInterviews, activeCandidates));
    }
}

// The four headline numbers shown on the dashboard. Tenant-scoped, computed per request.
public sealed record DashboardStatsDto(
    int OpenJobs, int NewApplicationsThisWeek, int UpcomingInterviews, int ActiveCandidates);
