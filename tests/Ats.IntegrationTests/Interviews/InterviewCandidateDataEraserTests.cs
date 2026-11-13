using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Interviews;

[Collection("Integration")]
public sealed class InterviewCandidateDataEraserTests
{
    private const string Notes = "Mira Tolun asked to keep the call short.";
    private const string CancellationNote = "Mira Tolun is travelling that week.";
    private const string Comments = "Mira Tolun explained the outbox pattern clearly.";
    private const int Rating = 4;

    private static readonly string[] WrittenAboutTheCandidate = [Notes, CancellationNote, Comments];

    private readonly PostgresContainerFixture _fixture;
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly FakeApplicationDirectory _applications = new(application: null);
    private readonly FixedCurrentUser _recruiter = new(Guid.NewGuid(), "recruiter@acme.test");

    public InterviewCandidateDataEraserTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task should_clear_what_each_company_wrote_about_the_candidate_and_keep_the_outcome()
    {
        var cancelled = await ScheduleAsync(Guid.NewGuid(), _accountId, cancel: true);
        var reviewed = await ScheduleAsync(Guid.NewGuid(), _accountId);
        var feedbackId = await SubmitFeedbackAsync(reviewed);

        await EraseAsync();

        var cancelledInterview = await LoadInterviewAsync(cancelled.InterviewId);
        Assert.Null(cancelledInterview.Notes);
        Assert.Null(cancelledInterview.CancellationNote);
        Assert.Equal(InterviewStatus.Cancelled, cancelledInterview.Status);
        Assert.Equal(InterviewCancellationReason.CandidateRequested, cancelledInterview.CancellationReason);
        Assert.Null((await LoadInterviewAsync(reviewed.InterviewId)).Notes);
        var feedback = await LoadFeedbackAsync(feedbackId);
        Assert.Null(feedback.Comments);
        Assert.Equal(Rating, feedback.Rating);
        Assert.Equal(FeedbackRecommendation.Hire, feedback.Recommendation);
    }

    [Fact]
    public async Task should_reach_an_interview_the_company_deleted()
    {
        var seeded = await ScheduleAsync(Guid.NewGuid(), _accountId);
        await DeleteAsync(seeded);

        await EraseAsync();

        Assert.Null((await LoadInterviewAsync(seeded.InterviewId)).Notes);
    }

    [Fact]
    public async Task should_leave_other_candidates_interviews_untouched()
    {
        var tenantId = Guid.NewGuid();
        await ScheduleAsync(tenantId, _accountId);
        var other = await ScheduleAsync(tenantId, Guid.NewGuid());
        var otherFeedbackId = await SubmitFeedbackAsync(other);

        await EraseAsync();

        Assert.Equal(Notes, (await LoadInterviewAsync(other.InterviewId)).Notes);
        Assert.Equal(Comments, (await LoadFeedbackAsync(otherFeedbackId)).Comments);
        Assert.All(
            (await AuditRowsAsync(tenantId)).Where(e => e.ResourceId == other.InterviewId.ToString()),
            entry => Assert.NotNull(entry.After));
    }

    // Who did what and when stays; the old values go.
    [Fact]
    public async Task should_clear_the_old_values_from_the_audit_log()
    {
        var seeded = await ScheduleAsync(Guid.NewGuid(), _accountId, cancel: true);
        await SubmitFeedbackAsync(seeded);
        var rowsBefore = await AuditRowsAsync(seeded.TenantId);

        await EraseAsync();

        var rows = await AuditRowsAsync(seeded.TenantId);
        Assert.Equal(rowsBefore.Select(e => (e.Id, e.Action)).Order(), rows.Select(e => (e.Id, e.Action)).Order());
        Assert.All(rows, entry => Assert.DoesNotContain(
            WrittenAboutTheCandidate, value => (entry.Before + entry.After).Contains(value, StringComparison.Ordinal)));
    }

    private async Task EraseAsync()
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        await new InterviewCandidateDataEraser(db, _applications).EraseAsync(_accountId);
    }

    // Saved inside the company the way a recruiter's request saves it, so the audit log records the text.
    private async Task<Seeded> ScheduleAsync(Guid tenantId, Guid accountId, bool cancel = false)
    {
        var applicationId = Guid.NewGuid();
        LinkApplication(accountId, applicationId);

        var nowUtc = DateTime.UtcNow;
        await using var db = NewDb(tenantId, _recruiter);
        var interview = Interview.Schedule(
            applicationId, InterviewType.Technical, nowUtc.AddDays(2), 30, [_recruiter.UserId!.Value], nowUtc, Notes);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();

        if (cancel)
        {
            interview.Cancel(InterviewCancellationReason.CandidateRequested, CancellationNote, nowUtc);
            await db.SaveChangesAsync();
        }

        return new Seeded(tenantId, interview.Id);
    }

    private async Task<Guid> SubmitFeedbackAsync(Seeded seeded)
    {
        await using var db = NewDb(seeded.TenantId, _recruiter);
        var feedback = InterviewFeedback.Submit(
            seeded.InterviewId, Guid.NewGuid(), Rating, FeedbackRecommendation.Hire, Comments);
        db.Feedback.Add(feedback);
        await db.SaveChangesAsync();
        return feedback.Id;
    }

    private async Task DeleteAsync(Seeded seeded)
    {
        await using var db = NewDb(seeded.TenantId, _recruiter);
        db.Interviews.Remove(await db.Interviews.SingleAsync(i => i.Id == seeded.InterviewId));
        await db.SaveChangesAsync();
    }

    private void LinkApplication(Guid accountId, Guid applicationId)
    {
        var linked = _applications.ApplicationIdsByCandidateAccount.GetValueOrDefault(accountId, []);
        _applications.ApplicationIdsByCandidateAccount[accountId] = [.. linked, applicationId];
    }

    private async Task<Interview> LoadInterviewAsync(Guid interviewId)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        return await db.Interviews.IgnoreQueryFilters().AsNoTracking().SingleAsync(i => i.Id == interviewId);
    }

    private async Task<InterviewFeedback> LoadFeedbackAsync(Guid feedbackId)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        return await db.Feedback.IgnoreQueryFilters().AsNoTracking().SingleAsync(f => f.Id == feedbackId);
    }

    private async Task<List<AuditLogEntry>> AuditRowsAsync(Guid tenantId)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        return await db.Set<AuditLogEntry>().AsNoTracking().Where(e => e.TenantId == tenantId).ToListAsync();
    }

    // Same interceptor order as Program.cs: tenant, auditable fields, then the audit log.
    private InterviewsDbContext NewDb(Guid? tenantId, ICurrentUser user)
    {
        var tenant = new FixedTenant(tenantId);
        var options = new DbContextOptionsBuilder<InterviewsDbContext>(
                PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant))
            .AddInterceptors(new AuditLogInterceptor(tenant, user, new HttpContextAccessor()))
            .Options;
        return new InterviewsDbContext(options, tenant);
    }

    private sealed record Seeded(Guid TenantId, Guid InterviewId);
}
