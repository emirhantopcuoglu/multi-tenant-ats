using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Notifications.Domain;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Npgsql;

namespace Ats.Api.IntegrationTests.CandidateAccounts;

// The account deletion and retention jobs against the app's own registrations, Postgres, Mongo and MinIO.
// Every value below is searched for afterwards, in every table and collection.
[Collection(ApiTestCollection.Name)]
public sealed class CandidateDataErasureTests
{
    private const string FirstName = "Selinay";
    private const string LastName = "Arvasoglu";
    private const string Phone = "+905550109988";
    private const string LinkedIn = "https://www.linkedin.com/in/selinay-arvasoglu";
    private const string CoverLetter = "Selinay Arvasoglu wants to join the platform team.";
    private const string RejectionReason = "Selinay needs more Kubernetes experience.";
    private const string Skill = "Quillmark templating";
    private const string CvFileName = "selinay-arvasoglu-cv.pdf";
    private const string InterviewNotes = "Selinay prefers to talk in the morning.";
    private const string CancellationNote = "Selinay asked for a later date.";
    private const string FeedbackComments = "Selinay explained the outbox pattern well.";

    private readonly AtsApiFactory _factory;
    private readonly string _email = $"selinay.arvasoglu.{Guid.NewGuid():N}@example.test";

    public CandidateDataErasureTests(AtsApiFactory factory) => _factory = factory;

    private string[] PersonalValues =>
    [
        _email, FirstName, LastName, Phone, LinkedIn, CoverLetter, RejectionReason, Skill, CvFileName,
        InterviewNotes, CancellationNote, FeedbackComments
    ];

    [Fact]
    public async Task nothing_of_an_erased_candidate_should_remain_in_the_databases_or_the_file_storage()
    {
        var tenantId = Guid.NewGuid();
        var (accountId, accountCvKey) = await SeedDueAccountAsync();
        var (applicationId, applicationCvKey) = await ApplyAsync(tenantId, accountId);
        await RejectAsync(tenantId, applicationId);
        await SeedDocumentsAsync(tenantId, applicationId);
        await InterviewAsync(tenantId, applicationId);
        await NotifyAsync(tenantId, accountId, applicationId);

        await RunDeletionJobAsync();

        Assert.Empty(await FindInPostgresAsync());
        Assert.Empty(await FindInMongoAsync());
        Assert.Empty(await FindFilesAsync(accountCvKey, applicationCvKey));
        Assert.False(await HasNotificationsAsync(accountId));
    }

    // Runs the publish through the outbox in a job scope, with no request behind it.
    [Fact]
    public async Task an_open_application_should_be_withdrawn_so_its_interviews_are_cancelled()
    {
        var tenantId = Guid.NewGuid();
        var (accountId, _) = await SeedDueAccountAsync();
        var (applicationId, _) = await ApplyAsync(tenantId, accountId);

        await RunDeletionJobAsync();

        var harness = _factory.Services.GetRequiredService<ITestHarness>();
        Assert.True(await harness.Published.Any<ApplicationWithdrawnIntegrationEvent>(
            message => message.Context.Message.ApplicationId == applicationId));
    }

    // A module missing here would keep the candidate's data after the account is gone.
    [Fact]
    public void every_module_that_keeps_candidate_data_should_have_an_eraser()
    {
        using var scope = _factory.Services.CreateScope();

        var erasers = scope.ServiceProvider.GetServices<ICandidateDataEraser>().Select(eraser => eraser.GetType());

        Assert.Equal(
            new HashSet<Type>
            {
                typeof(ApplicationCandidateDataEraser),
                typeof(InterviewCandidateDataEraser),
                typeof(NotificationCandidateDataEraser)
            },
            erasers.ToHashSet());
    }

    // No account row is seeded: the account outlives its applications and is not this job's business.
    [Fact]
    public async Task nothing_of_a_closed_application_should_remain_once_its_retention_period_ends()
    {
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var (applicationId, cvKey) = await ApplyAsync(tenantId, accountId);
        await RejectAsync(tenantId, applicationId);
        await SeedDocumentsAsync(tenantId, applicationId);
        await InterviewAsync(tenantId, applicationId);
        await NotifyAsync(tenantId, accountId, applicationId);
        await DateClosingBackAsync(applicationId);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ClosedApplicationRetentionJob>().EraseDueApplicationsAsync();

        Assert.Empty(await FindInPostgresAsync());
        Assert.Empty(await FindInMongoAsync());
        Assert.Empty(await FindFilesAsync(cvKey));
        Assert.False(await HasNotificationsAsync(accountId));
    }

    // A module missing here would keep its copy after the application's retention period.
    [Fact]
    public void every_module_that_keeps_application_data_should_have_an_eraser()
    {
        using var scope = _factory.Services.CreateScope();

        var erasers = scope.ServiceProvider.GetServices<IApplicationDataEraser>().Select(eraser => eraser.GetType());

        Assert.Equal(
            new HashSet<Type> { typeof(InterviewCandidateDataEraser), typeof(NotificationCandidateDataEraser) },
            erasers.ToHashSet());
    }

    private async Task DateClosingBackAsync(Guid applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var retention = scope.ServiceProvider.GetRequiredService<IOptions<RetentionOptions>>().Value;
        var closedAtUtc = DateTime.UtcNow.AddMonths(-retention.ClosedApplicationMonths).AddDays(-1);
        await scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>().Applications
            .IgnoreQueryFilters()
            .Where(a => a.Id == applicationId)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.ClosedAtUtc, closedAtUtc));
    }

    private async Task<(Guid AccountId, string CvKey)> SeedDueAccountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<ICandidatePasswordHasher>();
        var account = CandidateAccount.Register(
            _email, hasher.Hash($"Aa1!{Guid.NewGuid():N}"), FirstName, LastName, SupportedLanguages.Default);
        account.UpdateProfile(FirstName, LastName, Phone, "Türkiye", "İzmir", new DateOnly(1994, 3, 8));
        var cvKey = await UploadCvAsync(scope, $"candidates/{account.Id}/{Guid.NewGuid()}-{CvFileName}");
        account.AttachCv(cvKey, CvFileName);
        account.RequestDeletion(DateTime.UtcNow - CandidateAccount.DeletionGracePeriod);

        var db = scope.ServiceProvider.GetRequiredService<CandidateAccountsDbContext>();
        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();
        return (account.Id, cvKey);
    }

    // Saved inside a tenant, like a real apply request, so the audit log records every value.
    private async Task<(Guid ApplicationId, string CvKey)> ApplyAsync(Guid tenantId, Guid accountId)
    {
        using var scope = TenantScope(tenantId);
        var candidate = Candidate.Create(_email, FirstName, LastName, Phone, LinkedIn);
        var cvKey = await UploadCvAsync(scope, $"{tenantId}/{candidate.Id}/{Guid.NewGuid()}-{CvFileName}");
        var application = Application.Create(Guid.NewGuid(), candidate.Id, accountId, Guid.NewGuid(), cvKey, CoverLetter);

        var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        db.Candidates.Add(candidate);
        db.Applications.Add(application);
        await db.SaveChangesAsync();
        return (application.Id, cvKey);
    }

    private async Task RejectAsync(Guid tenantId, Guid applicationId)
    {
        using var scope = TenantScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        (await db.Applications.SingleAsync(a => a.Id == applicationId)).Reject(RejectionReason, Guid.NewGuid());
        await db.SaveChangesAsync();
    }

    private async Task SeedDocumentsAsync(Guid tenantId, Guid applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var activityLog = scope.ServiceProvider.GetRequiredService<IActivityLogRepository>();
        await activityLog.AddAsync(ApplicationActivity.Submitted(applicationId, Guid.NewGuid(), _email), tenantId);
        await activityLog.AddAsync(ApplicationActivity.Rejected(applicationId, Guid.NewGuid(), RejectionReason), tenantId);

        var parseResult = new CvParseResult(
            [Skill], 6, [new CvEducation("BSc Computer Engineering", "Ege University", 2016)],
            [new CvPosition("Platform Engineer", "Northwind", "2019", "2025")],
            CvJobFitRating.Strong, $"{FirstName} {LastName} fits the role.", [Skill], []);
        await scope.ServiceProvider.GetRequiredService<ICvParseResultRepository>()
            .SaveAsync(tenantId, applicationId, parseResult, DateTime.UtcNow);
    }

    private async Task InterviewAsync(Guid tenantId, Guid applicationId)
    {
        using var scope = TenantScope(tenantId);
        var nowUtc = DateTime.UtcNow;
        var interview = Interview.Schedule(
            applicationId, InterviewType.Technical, nowUtc.AddDays(2), 30, [Guid.NewGuid()], nowUtc, InterviewNotes);
        interview.Cancel(InterviewCancellationReason.CandidateRequested, CancellationNote, nowUtc);

        var db = scope.ServiceProvider.GetRequiredService<InterviewsDbContext>();
        db.Interviews.Add(interview);
        db.Feedback.Add(InterviewFeedback.Submit(
            interview.Id, Guid.NewGuid(), 4, FeedbackRecommendation.Hire, FeedbackComments));
        await db.SaveChangesAsync();
    }

    private async Task NotifyAsync(Guid tenantId, Guid accountId, Guid applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        db.Notifications.Add(Notification.ForCandidate(
            accountId, NotificationType.ApplicationViewed,
            NotificationPayloads.Serialize(new NotificationPayloads.ApplicationViewed(applicationId, "Platform Engineer"))));
        db.Notifications.Add(Notification.ForCompanyUser(
            tenantId, Guid.NewGuid(), NotificationType.NewApplication,
            NotificationPayloads.Serialize(
                new NotificationPayloads.NewApplication(applicationId, "Platform Engineer", FirstName, LastName))));
        await db.SaveChangesAsync();
    }

    private async Task<bool> HasNotificationsAsync(Guid accountId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Notifications
            .AnyAsync(n => n.RecipientId == accountId);
    }

    private async Task RunDeletionJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CandidateAccountDeletionJob>().EraseDueAccountsAsync();
    }

    private IServiceScope TenantScope(Guid tenantId)
    {
        var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId, "erasure-tests");
        return scope;
    }

    private static async Task<string> UploadCvAsync(IServiceScope scope, string key)
    {
        using var content = new MemoryStream("%PDF-1.4"u8.ToArray());
        await scope.ServiceProvider.GetRequiredService<IFileStorage>()
            .UploadAsync(key, content, content.Length, "application/pdf");
        return key;
    }

    // Every row as text, so a column added later is searched too.
    private async Task<List<string>> FindInPostgresAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var connection = (NpgsqlConnection)scope.ServiceProvider
            .GetRequiredService<ApplicationsDbContext>().Database.GetDbConnection();
        await connection.OpenAsync();

        var found = new List<string>();
        foreach (var table in await ListTablesAsync(connection))
        {
            foreach (var value in PersonalValues)
            {
                await using var command = new NpgsqlCommand(
                    $"SELECT count(*) FROM {table} t WHERE strpos(lower(t::text), lower(@value)) > 0", connection);
                command.Parameters.AddWithValue("value", value);
                if ((long)(await command.ExecuteScalarAsync())! > 0)
                    found.Add($"{table}: {value}");
            }
        }

        return found;
    }

    // Every schema but Postgres' own, so a module added later is searched too.
    private static async Task<List<string>> ListTablesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT format('%I.%I', table_schema, table_name) FROM information_schema.tables " +
            "WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')",
            connection);

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));
        return tables;
    }

    private async Task<List<string>> FindInMongoAsync()
    {
        var database = _factory.Services.GetRequiredService<IMongoDatabase>();
        var found = new List<string>();
        foreach (var name in await (await database.ListCollectionNamesAsync()).ToListAsync())
        {
            var documents = await database.GetCollection<BsonDocument>(name)
                .Find(FilterDefinition<BsonDocument>.Empty)
                .ToListAsync();
            found.AddRange(
                from document in documents
                from value in PersonalValues
                where document.ToJson().Contains(value, StringComparison.OrdinalIgnoreCase)
                select $"{name}: {value}");
        }

        return found;
    }

    private async Task<List<string>> FindFilesAsync(params string[] keys)
    {
        using var scope = _factory.Services.CreateScope();
        var found = new List<string>();
        await foreach (var file in scope.ServiceProvider.GetRequiredService<IFileStorage>().ListAsync())
        {
            if (keys.Contains(file.Key))
                found.Add(file.Key);
        }

        return found;
    }
}
