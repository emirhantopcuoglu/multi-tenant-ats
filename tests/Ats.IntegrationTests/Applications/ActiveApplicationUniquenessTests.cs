using System.Text;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.IntegrationTests.Applications;

// "One active application per (candidate, job)" used to live only in SubmitApplicationHandler, as a
// read followed by an insert. Two submits that interleave between those two steps both pass the
// read and both insert. Candidates already had the same rule enforced at the database for
// (tenant, email); Applications now does too, through a partial unique index.
//
// These must run against real PostgreSQL: a partial index is the whole subject, and no in-memory
// provider enforces one.
[Collection("Integration")]
public sealed class ActiveApplicationUniquenessTests
{
    private readonly PostgresContainerFixture _fixture;

    public ActiveApplicationUniquenessTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task The_database_should_reject_a_second_active_application()
    {
        // Deliberately bypasses the handler. The point is that the rule holds even when the
        // application-level check is not the thing standing in front of it.
        var tenant = new FixedTenant(Guid.NewGuid());
        var (jobId, candidateId, stageId) = await SeedAsync(tenant);

        await InsertApplicationAsync(tenant, jobId, candidateId, stageId);

        var second = await Record.ExceptionAsync(
            () => InsertApplicationAsync(tenant, jobId, candidateId, stageId));

        Assert.IsType<DbUpdateException>(second);
    }

    [Fact]
    public async Task A_withdrawn_application_should_not_block_a_new_one()
    {
        // The reason the index is partial. A candidate who withdrew, or was rejected, is free to
        // apply again — a plain unique index on the three columns would lock them out for good, and
        // would have made the withdrawal feature a trap rather than a courtesy.
        var tenant = new FixedTenant(Guid.NewGuid());
        var (jobId, candidateId, stageId) = await SeedAsync(tenant);

        var firstId = await InsertApplicationAsync(tenant, jobId, candidateId, stageId);
        await using (var db = NewDb(tenant))
        {
            var stored = await db.Applications.SingleAsync(a => a.Id == firstId);
            stored.Withdraw();
            await db.SaveChangesAsync();
        }

        var secondId = await InsertApplicationAsync(tenant, jobId, candidateId, stageId);

        Assert.NotEqual(firstId, secondId);
        await using (var db = NewDb(tenant))
        {
            Assert.Equal(2, await db.Applications.CountAsync(a => a.CandidateId == candidateId));
        }
    }

    [Fact]
    public async Task Another_tenant_applying_to_its_own_job_should_be_unaffected()
    {
        // TenantId leads the index, so the constraint is per-company. Worth pinning: an index that
        // accidentally spanned tenants would let one company's data refuse another's write.
        var first = new FixedTenant(Guid.NewGuid());
        var second = new FixedTenant(Guid.NewGuid());
        var (jobId, candidateId, stageId) = await SeedAsync(first);
        await InsertApplicationAsync(first, jobId, candidateId, stageId);

        var (otherJobId, otherCandidateId, otherStageId) = await SeedAsync(second);
        var otherId = await InsertApplicationAsync(second, otherJobId, otherCandidateId, otherStageId);

        Assert.NotEqual(Guid.Empty, otherId);
    }

    [Fact]
    public async Task A_submit_that_loses_the_race_should_be_refused_rather_than_crash()
    {
        // The index alone would turn the loser's 200 into a 500 with a constraint name in it. The
        // handler catches the failure and asks the database the same question its pre-check asked,
        // so the loser gets the ordinary duplicate error.
        //
        // The race is made deterministic with an interceptor: the rival application is inserted on
        // its own connection at the moment the handler calls SaveChanges — after its pre-check has
        // already read "no application yet", which is exactly the window that used to be open.
        var tenantId = Guid.NewGuid();
        var tenant = new FixedTenant(tenantId);
        var email = $"{Guid.NewGuid():N}@acme.test";
        var job = new PublishedJob(Guid.NewGuid(), "Staff Engineer", "staff-engineer");

        Guid candidateId;
        Guid stageId;
        await using (var db = NewDb(tenant))
        {
            var pipeline = Pipeline.CreateDefault(job.Id);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create(email, "Race", "Loser");
            db.Candidates.Add(candidate);
            await db.SaveChangesAsync();
            candidateId = candidate.Id;
            stageId = pipeline.InitialStage.Id;
        }

        var interceptor = new InsertRivalOnFirstSave(
            () => InsertApplicationAsync(tenant, job.Id, candidateId, stageId));

        await using var handlerDb = new ApplicationsDbContext(
            new DbContextOptionsBuilder<ApplicationsDbContext>()
                .UseNpgsql(
                    _fixture.ConnectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "applications"))
                .AddInterceptors(
                    new TenantSaveChangesInterceptor(tenant),
                    new AuditableSaveChangesInterceptor(new NullCurrentUser()),
                    interceptor)
                .Options,
            tenant);

        var accountId = Guid.NewGuid();
        var storage = new RecordingFileStorage();
        var handler = new SubmitApplicationHandler(
            handlerDb,
            new StubPublishedJobDirectory(job),
            new StubCandidateAccountReader(new CandidateAccountSummary(
                accountId, email, "Race", "Loser", IsEmailVerified: true,
                CvFileKey: null, CvFileName: null)),
            storage,
            tenant,
            new CapturingPublisher(),
            new InMemoryActivityLog([]),
            NullLogger<SubmitApplicationHandler>.Instance);

        var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 pretend cv"));
        var result = await handler.Handle(
            new SubmitApplicationCommand(
                job.Slug, accountId, null, null, null,
                new CvUpload(content, content.Length, "application/pdf", "cv.pdf")),
            CancellationToken.None);

        Assert.True(interceptor.Fired);
        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.DuplicateApplication.Code, result.Error.Code);

        // Only the winner's row survives, and the loser's uploaded CV is cleaned up rather than left
        // orphaned in the bucket — the same compensation the handler already did for other failures.
        await using (var db = NewDb(tenant))
        {
            Assert.Equal(1, await db.Applications.CountAsync(a => a.CandidateId == candidateId));
        }
        Assert.Equal(storage.Uploaded, storage.Deleted);
    }

    private async Task<(Guid JobId, Guid CandidateId, Guid StageId)> SeedAsync(FixedTenant tenant)
    {
        var jobId = Guid.NewGuid();
        await using var db = NewDb(tenant);

        var pipeline = Pipeline.CreateDefault(jobId);
        db.Pipelines.Add(pipeline);
        var candidate = Candidate.Create($"{Guid.NewGuid():N}@acme.test", "Ada", "Applicant");
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();

        return (jobId, candidate.Id, pipeline.InitialStage.Id);
    }

    private async Task<Guid> InsertApplicationAsync(
        FixedTenant tenant, Guid jobId, Guid candidateId, Guid stageId)
    {
        await using var db = NewDb(tenant);
        var application = ApplicationEntity.Create(
            jobId, candidateId, Guid.NewGuid(), stageId, "cv/race.pdf");
        db.Applications.Add(application);
        await db.SaveChangesAsync();
        return application.Id;
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);

    // Runs a rival write on its own connection the first time the intercepted context saves, which
    // puts the conflicting row in place between the handler's pre-check and its insert.
    private sealed class InsertRivalOnFirstSave : SaveChangesInterceptor
    {
        private readonly Func<Task> _insertRival;

        public InsertRivalOnFirstSave(Func<Task> insertRival) => _insertRival = insertRival;

        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!Fired)
            {
                Fired = true;
                await _insertRival();
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
