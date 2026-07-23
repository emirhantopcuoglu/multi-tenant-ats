using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.Interviews;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Contracts.Tenants;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class ListCandidateInterviewsTests
{
    private readonly PostgresContainerFixture _fixture;

    public ListCandidateInterviewsTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_only_the_calling_candidates_interviews_across_applications()
    {
        // Arrange — two applications for the same candidate account, in different tenants, each with
        // an interview; a third application belonging to a different candidate must never show up.
        var candidateAccountId = Guid.NewGuid();
        var otherCandidateAccountId = Guid.NewGuid();
        var tenantA = new FixedTenant(Guid.NewGuid());
        var tenantB = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Application applicationA, applicationB, applicationOther;
        await using (var db = NewDb(tenantA))
        {
            var pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("mine@acme.test", "My", "Self");
            db.Candidates.Add(candidate);
            applicationA = Application.Create(
                jobId, candidate.Id, candidateAccountId, pipeline.InitialStage.Id, "cv/a.pdf");
            db.Applications.Add(applicationA);

            var otherCandidate = Candidate.Create("other@acme.test", "Other", "Person");
            db.Candidates.Add(otherCandidate);
            applicationOther = Application.Create(
                jobId, otherCandidate.Id, otherCandidateAccountId, pipeline.InitialStage.Id, "cv/other.pdf");
            db.Applications.Add(applicationOther);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb(tenantB))
        {
            var pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("mine@acme.test", "My", "Self");
            db.Candidates.Add(candidate);
            applicationB = Application.Create(
                jobId, candidate.Id, candidateAccountId, pipeline.InitialStage.Id, "cv/b.pdf");
            db.Applications.Add(applicationB);
            await db.SaveChangesAsync();
        }

        var scheduledAt = DateTime.UtcNow.AddDays(2);
        var interviews = new List<CandidateInterviewInfo>
        {
            new(Guid.NewGuid(), applicationA.Id, "Technical", scheduledAt, 60, "Scheduled", "token-a"),
            new(Guid.NewGuid(), applicationB.Id, "Final", scheduledAt.AddDays(1), 30, "Scheduled", "token-b"),
            // Belongs to a different candidate's application — the handler must never fetch or
            // surface this one, but the fake returns it unconditionally to prove that filtering
            // happens before the interview lookup, not after.
            new(Guid.NewGuid(), applicationOther.Id, "PhoneScreen", scheduledAt, 30, "Scheduled", "token-other"),
        };

        // Act
        await using var readDb = NewDb(tenantA);
        var handler = new ListCandidateInterviewsHandler(
            readDb,
            new FakeJobDirectory(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenantA.TenantId!.Value)),
            new FakeTenantDirectory(new TenantSummary(tenantA.TenantId!.Value, "Acme", "acme")),
            new FakeInterviewDirectory(interviews));
        var result = await handler.Handle(new ListCandidateInterviewsQuery(candidateAccountId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.DoesNotContain(result.Value, i => i.ApplicationId == applicationOther.Id);
        Assert.Contains(result.Value, i => i.ApplicationId == applicationA.Id && i.RoomToken == "token-a");
        Assert.Contains(result.Value, i => i.ApplicationId == applicationB.Id && i.RoomToken == "token-b");
        // Newest scheduled time first.
        Assert.Equal(applicationB.Id, result.Value[0].ApplicationId);
    }

    [Fact]
    public async Task should_return_empty_when_the_candidate_has_no_applications()
    {
        await using var readDb = NewDb(new FixedTenant(Guid.NewGuid()));
        var handler = new ListCandidateInterviewsHandler(
            readDb, new FakeJobDirectory(null), new FakeTenantDirectory(null), new FakeInterviewDirectory([]));

        var result = await handler.Handle(
            new ListCandidateInterviewsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
