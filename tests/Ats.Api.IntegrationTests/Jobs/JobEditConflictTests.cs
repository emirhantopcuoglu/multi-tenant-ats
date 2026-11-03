using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Tenants.Domain;

namespace Ats.Api.IntegrationTests.Jobs;

[Collection(ApiTestCollection.Name)]
public sealed class JobEditConflictTests
{
    private const string JobsUrl = "/api/v1/jobs";

    private readonly AtsApiFactory _factory;

    public JobEditConflictTests(AtsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task should_answer_409_to_a_save_from_a_form_loaded_before_someone_else_saved()
    {
        var client = _factory.CreateCompanyClient(Roles.Admin, Guid.NewGuid());
        var created = await client.PostAsJsonAsync(JobsUrl, JobBody("Backend Developer"));
        created.EnsureSuccessStatusCode();
        var jobUrl = created.Headers.Location!.PathAndQuery;
        var loadedByBoth = await client.GetFromJsonAsync<JsonElement>(jobUrl);
        var version = loadedByBoth.GetProperty("version").GetUInt32();

        var first = await client.PutAsJsonAsync(jobUrl, JobBody("Senior Backend Developer", version));
        var second = await client.PutAsJsonAsync(jobUrl, JobBody("Backend Engineer", version));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("concurrency_conflict", problem.GetProperty("code").GetString());
    }

    private static object JobBody(string title, uint version = 0) => new
    {
        title,
        description = "Build APIs",
        department = "Engineering",
        city = "Istanbul",
        country = "Turkey",
        employmentType = "FullTime",
        experienceLevel = "Mid",
        workArrangement = "Remote",
        salaryMin = (decimal?)null,
        salaryMax = (decimal?)null,
        salaryCurrency = (string?)null,
        version,
    };
}
