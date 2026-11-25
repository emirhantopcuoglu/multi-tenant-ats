using Ats.Api.Extensions;
using Ats.Api.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Ats.Api.IntegrationTests.Persistence;

// The modules migrate one after another, so the last one failing used to leave the first five applied.
[Collection(ApiTestCollection.Name)]
public sealed class MigrationAtomicityTests
{
    private readonly AtsApiFactory _factory;

    public MigrationAtomicityTests(AtsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task a_failing_module_should_leave_the_database_untouched()
    {
        var connectionString = await CreateDatabaseAsync();
        // The last module's first migration creates this table, so it fails there.
        await ExecuteAsync(connectionString, """CREATE SCHEMA notifications; CREATE TABLE notifications."Notifications" (id int);""");

        await Assert.ThrowsAnyAsync<Exception>(() => MigrateAsync(connectionString));

        Assert.Null(await ScalarAsync(connectionString, """SELECT to_regclass('tenants."__EFMigrationsHistory"')::text"""));
    }

    private async Task MigrateAsync(string connectionString)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Postgres"] = connectionString;
        builder.Configuration["Mongo:ConnectionString"] =
            _factory.Services.GetRequiredService<IConfiguration>()["Mongo:ConnectionString"];
        builder.AddPersistence();

        using var host = builder.Build();
        await DatabaseMigrator.MigrateAsync(host.Services);
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var server = _factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")!;
        var name = $"migrate_{Guid.NewGuid():N}";
        await ExecuteAsync(server, $"CREATE DATABASE {name}");
        return new NpgsqlConnectionStringBuilder(server) { Database = name, Pooling = false }.ConnectionString;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }
}
