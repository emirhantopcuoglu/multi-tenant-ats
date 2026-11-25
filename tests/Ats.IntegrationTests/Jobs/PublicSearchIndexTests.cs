using System.Data.Common;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Jobs.Application.Jobs;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Ats.IntegrationTests.Jobs;

// Postgres only uses an expression index when the query filters on exactly that expression, so a change
// on either side would quietly bring back the full table scan. On 100k jobs a search with no match took
// about 130 ms without the index and under 1 ms with it.
[Collection("Integration")]
public sealed class PublicSearchIndexTests
{
    private readonly PostgresContainerFixture _fixture;

    public PublicSearchIndexTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("""lower(j."Title") LIKE""", "jobs", """USING gin (lower(("Title")::text) gin_trgm_ops)""")]
    [InlineData("""lower(t."Name") LIKE""", "tenants", """USING gin (lower(("Name")::text) gin_trgm_ops)""")]
    public async Task the_feed_search_should_filter_on_an_indexed_expression(
        string filter, string schema, string indexDefinition)
    {
        var recorder = new CommandRecorder();

        await SearchAsync(recorder);

        Assert.Contains(recorder.Commands, sql => sql.Contains(filter));
        Assert.Contains(await IndexDefinitionsAsync(schema), definition => definition.EndsWith(indexDefinition));
    }

    private async Task SearchAsync(CommandRecorder recorder)
    {
        var tenant = new FixedTenant(null);
        var jobsOptions = new DbContextOptionsBuilder<JobsDbContext>(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant)).AddInterceptors(recorder).Options;
        var tenantsOptions = new DbContextOptionsBuilder<TenantsDbContext>(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant)).AddInterceptors(recorder).Options;
        await using var jobsDb = new JobsDbContext(jobsOptions, tenant);
        await using var tenantsDb = new TenantsDbContext(tenantsOptions, tenant);

        await new ListPublicJobFeedHandler(jobsDb, new TenantDirectory(tenantsDb))
            .Handle(new ListPublicJobFeedQuery(Search: "engineer"), CancellationToken.None);
    }

    private async Task<List<string>> IndexDefinitionsAsync(string schema)
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT indexdef FROM pg_indexes WHERE schemaname = @schema", conn);
        command.Parameters.AddWithValue("schema", schema);
        var definitions = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            definitions.Add(reader.GetString(0));
        return definitions;
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
