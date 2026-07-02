namespace Ats.IntegrationTests.Shared;

[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresContainerFixture>
{
}
