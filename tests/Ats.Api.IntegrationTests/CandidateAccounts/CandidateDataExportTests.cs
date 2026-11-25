using System.Net;
using System.Text.Json;
using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Notifications.Domain;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.CandidateAccounts;

// A candidate's copy of what is kept about them (KVKK right of access).
[Collection(ApiTestCollection.Name)]
public sealed class CandidateDataExportTests
{
    private const string Url = "/api/v1/candidate/account/export";
    private const string CoverLetter = "Kuzey ekibinde veri hatlarını sahiplenmek isterim.";

    private readonly AtsApiFactory _factory;

    public CandidateDataExportTests(AtsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task the_export_should_hold_the_candidates_data_from_every_module()
    {
        var (client, accountId) = await _factory.CreateCandidateClientAsync();
        var tenantId = Guid.NewGuid();
        var applicationId = await ApplyAsync(tenantId, accountId);
        await InterviewAsync(tenantId, applicationId);
        await NotifyAsync(accountId, applicationId);

        using var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        using var export = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = export.RootElement;
        Assert.Equal(accountId.ToString(), root.GetProperty("account").GetProperty("id").GetString());
        var application = Assert.Single(root.GetProperty("applications").EnumerateArray());
        Assert.Equal(CoverLetter, application.GetProperty("coverLetter").GetString());
        Assert.Equal("Technical", Assert.Single(root.GetProperty("interviews").EnumerateArray()).GetProperty("type").GetString());
        Assert.Equal("ApplicationViewed", Assert.Single(root.GetProperty("notifications").EnumerateArray()).GetProperty("type").GetString());
    }

    // A module that keeps candidate data without an exporter leaves it out of the export unnoticed.
    [Fact]
    public void every_module_that_keeps_candidate_data_should_export_it()
    {
        using var scope = _factory.Services.CreateScope();

        var exporters = scope.ServiceProvider.GetServices<ICandidateDataExporter>().Select(e => e.GetType());

        Assert.Equal(
            new HashSet<Type>
            {
                typeof(CandidateAccountDataExporter),
                typeof(ApplicationCandidateDataExporter),
                typeof(InterviewCandidateDataExporter),
                typeof(NotificationCandidateDataExporter)
            },
            exporters.ToHashSet());
    }

    [Fact]
    public async Task another_candidates_data_should_not_be_in_the_export()
    {
        var (_, otherAccountId) = await _factory.CreateCandidateClientAsync();
        await ApplyAsync(Guid.NewGuid(), otherAccountId);
        var (client, _) = await _factory.CreateCandidateClientAsync();

        using var response = await client.GetAsync(Url);

        using var export = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(export.RootElement.GetProperty("applications").EnumerateArray());
    }

    private async Task<Guid> ApplyAsync(Guid tenantId, Guid accountId)
    {
        using var scope = TenantScope(tenantId);
        var candidate = Candidate.Create($"aday-{Guid.NewGuid():N}@example.test", "Deniz", "Aksoy");
        var application = Application.Create(
            Guid.NewGuid(), candidate.Id, accountId, Guid.NewGuid(), $"{tenantId}/{candidate.Id}/cv.pdf", CoverLetter);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        db.Candidates.Add(candidate);
        db.Applications.Add(application);
        await db.SaveChangesAsync();
        return application.Id;
    }

    private async Task InterviewAsync(Guid tenantId, Guid applicationId)
    {
        using var scope = TenantScope(tenantId);
        var nowUtc = DateTime.UtcNow;
        var db = scope.ServiceProvider.GetRequiredService<InterviewsDbContext>();
        db.Interviews.Add(Interview.Schedule(
            applicationId, InterviewType.Technical, nowUtc.AddDays(2), 45, [Guid.NewGuid()], nowUtc, "Internal note"));
        await db.SaveChangesAsync();
    }

    private async Task NotifyAsync(Guid accountId, Guid applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        db.Notifications.Add(Notification.ForCandidate(
            accountId, NotificationType.ApplicationViewed,
            NotificationPayloads.Serialize(new NotificationPayloads.ApplicationViewed(applicationId, "Data Engineer"))));
        await db.SaveChangesAsync();
    }

    private IServiceScope TenantScope(Guid tenantId)
    {
        var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId, "export-tests");
        return scope;
    }
}
