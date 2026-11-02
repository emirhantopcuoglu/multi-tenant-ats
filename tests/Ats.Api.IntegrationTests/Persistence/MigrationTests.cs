using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.Persistence;

// Migrate() throws on pending changes too, but not once PendingModelChangesWarning is suppressed.
[Collection(ApiTestCollection.Name)]
public sealed class MigrationTests
{
    private readonly AtsApiFactory _factory;

    public MigrationTests(AtsApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<Type> DbContextTypes => new()
    {
        typeof(TenantsDbContext),
        typeof(JobsDbContext),
        typeof(ApplicationsDbContext),
        typeof(InterviewsDbContext),
        typeof(CandidateAccountsDbContext),
        typeof(NotificationsDbContext)
    };

    [Theory]
    [MemberData(nameof(DbContextTypes))]
    public void should_have_a_migration_for_every_model_change(Type dbContextType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(dbContextType);

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
