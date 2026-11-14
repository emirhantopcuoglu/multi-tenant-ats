using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.Persistence;

[Collection(ApiTestCollection.Name)]
public sealed class RetentionCleanupWiringTests
{
    private readonly AtsApiFactory _factory;

    public RetentionCleanupWiringTests(AtsApiFactory factory) => _factory = factory;

    // A module missing here keeps its expired records for good. Every schema with an AuditLogs table needs one.
    [Fact]
    public void every_module_that_keeps_expiring_records_should_have_a_cleaner()
    {
        using var scope = _factory.Services.CreateScope();

        var cleaners = scope.ServiceProvider.GetServices<IRetentionCleaner>().Select(cleaner => cleaner.GetType());

        Assert.Equal(
            new HashSet<Type>
            {
                typeof(AuditLogRetentionCleaner<TenantsDbContext>),
                typeof(AuditLogRetentionCleaner<JobsDbContext>),
                typeof(AuditLogRetentionCleaner<ApplicationsDbContext>),
                typeof(AuditLogRetentionCleaner<InterviewsDbContext>),
                typeof(TenantsRetentionCleaner),
                typeof(CandidateAccountsRetentionCleaner),
                typeof(NotificationRetentionCleaner)
            },
            cleaners.ToHashSet());
    }

    [Fact]
    public async Task the_nightly_job_should_run_against_the_real_registrations()
    {
        using var scope = _factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<RetentionCleanupJob>().CleanupAsync();
    }
}
