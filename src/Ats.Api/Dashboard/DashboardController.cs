using Asp.Versioning;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Interviews;
using Ats.Shared.Contracts.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Api.Dashboard;

// The four numbers come from three modules, so they are combined here in the API root through the
// modules' read ports.
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard")]
[ApiVersion("1.0")]
public sealed class DashboardController : ControllerBase
{
    // A rolling 7 days avoids week-boundary and timezone questions.
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

public sealed record DashboardStatsDto(
    int OpenJobs, int NewApplicationsThisWeek, int UpcomingInterviews, int ActiveCandidates);
