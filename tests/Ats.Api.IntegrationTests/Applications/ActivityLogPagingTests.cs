using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ats.Api.IntegrationTests.Applications;

// Against a real Mongo, with every entry on the same timestamp: the case where paging could repeat or
// skip one.
[Collection(ApiTestCollection.Name)]
public sealed class ActivityLogPagingTests
{
    private readonly AtsApiFactory _factory;

    public ActivityLogPagingTests(AtsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task pages_should_cover_every_entry_once()
    {
        var tenantId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var database = _factory.Services.GetRequiredService<IMongoDatabase>();
        var repository = new MongoActivityLogRepository(database, new Tenant(tenantId));
        for (var i = 0; i < 25; i++)
            await repository.AddAsync(ApplicationActivity.Viewed(applicationId, actorUserId: null));
        await database.GetCollection<BsonDocument>("application_activities").UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("applicationId", applicationId.ToString()),
            Builders<BsonDocument>.Update.Set("occurredAtUtc", DateTime.UtcNow));

        var pages = new[]
        {
            await repository.GetPageByApplicationAsync(applicationId, page: 1, pageSize: 10),
            await repository.GetPageByApplicationAsync(applicationId, page: 2, pageSize: 10),
            await repository.GetPageByApplicationAsync(applicationId, page: 3, pageSize: 10),
        };

        Assert.All(pages, p => Assert.Equal(25, p.TotalCount));
        Assert.Equal([10, 10, 5], pages.Select(p => p.Items.Count));
        var entries = pages.SelectMany(p => p.Items).ToList();
        Assert.Equal(25, entries.Select(e => e.Id).Distinct().Count());
    }

    private sealed class Tenant(Guid tenantId) : ICurrentTenant
    {
        public Guid? TenantId => tenantId;
    }
}
