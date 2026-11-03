namespace Ats.Api.IntegrationTests.Infrastructure;

// One API and one set of containers for all API tests.
[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<AtsApiFactory>
{
    public const string Name = "Api";
}
