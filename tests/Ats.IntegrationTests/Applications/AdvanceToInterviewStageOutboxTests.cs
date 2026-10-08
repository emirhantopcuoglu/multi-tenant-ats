using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ats.IntegrationTests.Applications;

// AdvanceToInterviewStageConsumerTests exercises AdvanceAsync directly, which is enough for the
// stage rules but says nothing about the bus. This file runs the consumer through a real MassTransit
// harness instead, because the defect it covers lived entirely in the wiring:
//
// the consumer committed the stage move and then published to the broker as a separate step, so a
// publish failure lost the announcement for good — the retry re-read an application that was already
// in the Interview stage, decided there was nothing to do, and returned before publishing.
//
// The endpoint now runs behind the transactional outbox, which is a configuration concern nothing
// else in the suite covers: the API host is never started in CI, so a broken bus registration
// otherwise reaches production unchallenged.
[Collection("Integration")]
public sealed class AdvanceToInterviewStageOutboxTests
{
    private readonly PostgresContainerFixture _fixture;

    public AdvanceToInterviewStageOutboxTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_announce_the_stage_move_it_just_committed()
    {
        // Arrange — an application sitting at the pipeline's first stage.
        var tenantId = Guid.NewGuid();
        var (application, pipeline) = await SeedAsync(tenantId);
        var interviewStage = pipeline.Stages.Single(s => s.Name == "Interview");

        using var host = BuildHarness(out var activityLog);
        await host.StartAsync();
        var harness = host.Services.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Act — the message a recruiter's "schedule interview" click produces.
        var messageId = NewId.NextGuid();
        await harness.Bus.Publish(
            EventFor(application.Id, tenantId), context => context.MessageId = messageId);

        // Assert — consumed, moved, and announced. The announcement is the part that used to go
        // missing, so its absence must fail this test rather than show up as a quiet log line.
        Assert.True(await harness.Consumed.Any<InterviewScheduledIntegrationEvent>());
        Assert.True(await harness.Consumed.Any<ApplicationStageChangedIntegrationEvent>());

        await using (var db = NewDb(tenantId))
        {
            var stored = await db.Applications.SingleAsync(a => a.Id == application.Id);
            Assert.Equal(interviewStage.Id, stored.CurrentStageId);
        }

        var announcement = harness.Consumed
            .Select<ApplicationStageChangedIntegrationEvent>()
            .Single()
            .Context.Message;
        Assert.Equal(application.Id, announcement.ApplicationId);
        Assert.Equal(pipeline.InitialStage.Id, announcement.FromStageId);
        Assert.Equal(interviewStage.Id, announcement.ToStageId);
        Assert.Equal(tenantId, announcement.TenantId);

        Assert.Single(activityLog.Added);

        // The assertions above pass with or without the outbox — on the happy path both wirings end
        // with the announcement delivered. This one does not: an InboxState row is written only by
        // the endpoint's outbox filter, so it fails the moment the consumer stops being registered
        // with AdvanceToInterviewStageConsumerDefinition, which is the whole fix.
        await using (var probe = NewDb(tenantId))
        {
            var inboxRows = await probe.Database
                .SqlQueryRaw<long>(
                    """SELECT COUNT(*) AS "Value" FROM applications."InboxState" WHERE "MessageId" = {0}""",
                    messageId)
                .SingleAsync();
            Assert.Equal(1, inboxRows);
        }
    }

    private static InterviewScheduledIntegrationEvent EventFor(Guid applicationId, Guid tenantId) =>
        new(
            InterviewId: Guid.NewGuid(),
            ApplicationId: applicationId,
            JobId: Guid.NewGuid(),
            JobTitle: "Staff Engineer",
            CandidateId: Guid.NewGuid(),
            CandidateAccountId: Guid.NewGuid(),
            CandidateEmail: "outbox@acme.test",
            CandidateFirstName: "Out",
            InterviewType: "Technical",
            ScheduledAtUtc: DateTime.UtcNow.AddDays(3),
            DurationMinutes: 60,
            RoomToken: null,
            TenantId: tenantId);

    // Mirrors the host's registration closely enough to be worth something: same DbContext, same
    // IApplicationsDbContext aliasing onto it (the outbox and the consumer must share one instance,
    // or the publish lands outside the consumer's transaction), same consumer definition.
    //
    // Built as a real host rather than a bare ServiceProvider because the outbox's delivery service
    // is an IHostedService. Without it the message stops in the OutboxMessage table and the test
    // cannot tell "captured then delivered" apart from "never published at all".
    private IHost BuildHarness(out InMemoryActivityLog activityLog)
    {
        var log = new InMemoryActivityLog([]);
        activityLog = log;

        var builder = Host.CreateApplicationBuilder();
        var services = builder.Services;
        services.AddSingleton<ICurrentTenant>(new FixedTenant(null));
        services.AddDbContext<ApplicationsDbContext>((sp, options) => options
            .UseNpgsql(
                _fixture.ConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "applications"))
            .AddInterceptors(
                new TenantSaveChangesInterceptor(sp.GetRequiredService<ICurrentTenant>()),
                new AuditableSaveChangesInterceptor(new NullCurrentUser())));
        services.AddScoped<IApplicationsDbContext>(sp => sp.GetRequiredService<ApplicationsDbContext>());
        services.AddSingleton<IActivityLogRepository>(log);

        services.AddMassTransitTestHarness(bus =>
        {
            bus.AddEntityFrameworkOutbox<ApplicationsDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                // Production leaves this at its default; a second of polling would make the test
                // look hung for no benefit.
                outbox.QueryDelay = TimeSpan.FromMilliseconds(200);
            });
            bus.AddConsumer<AdvanceToInterviewStageConsumer, AdvanceToInterviewStageConsumerDefinition>();
            // Stands in for the real subscribers (stage-change email, in-app notification). The
            // property under test is that the announcement reaches a subscriber at all, which is
            // what stopped happening when a publish failure was retried away.
            bus.AddConsumer<StageChangedProbeConsumer>();
        });

        return builder.Build();
    }

    private async Task<(Application Application, Pipeline Pipeline)> SeedAsync(Guid tenantId)
    {
        var jobId = Guid.NewGuid();
        await using var db = NewDb(tenantId);

        var pipeline = Pipeline.CreateDefault(jobId);
        db.Pipelines.Add(pipeline);
        var candidate = Candidate.Create("outbox@acme.test", "Out", "Box");
        db.Candidates.Add(candidate);
        var application = Application.Create(
            jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/outbox.pdf");
        db.Applications.Add(application);
        await db.SaveChangesAsync();

        return (application, pipeline);
    }

    private ApplicationsDbContext NewDb(Guid tenantId)
    {
        var tenant = new FixedTenant(tenantId);
        return new ApplicationsDbContext(
            PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
    }
}

// A stand-in subscriber. The real ones live in the Notifications module and would drag SMTP and the
// notifications schema into a test about message plumbing.
public sealed class StageChangedProbeConsumer : IConsumer<ApplicationStageChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ApplicationStageChangedIntegrationEvent> context) =>
        Task.CompletedTask;
}
