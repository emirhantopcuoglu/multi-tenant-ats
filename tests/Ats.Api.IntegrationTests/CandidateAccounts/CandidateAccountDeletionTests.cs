using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.CandidateAccounts;

[Collection(ApiTestCollection.Name)]
public sealed class CandidateAccountDeletionTests
{
    private const string Password = "Correct-Horse-9";

    private readonly AtsApiFactory _factory;

    public CandidateAccountDeletionTests(AtsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task a_deletion_request_should_wait_two_weeks_and_be_cancellable_by_signing_in()
    {
        var client = NewClient();
        var email = $"candidate-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/v1/candidate/auth/register", new
        {
            email,
            password = Password,
            firstName = "Mira",
            lastName = "Tolun",
        });
        var firstToken = await AccessTokenFromAsync(register);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/candidate/account")
        {
            Content = JsonContent.Create(new { currentPassword = Password }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var deletion = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        var dueAt = (await deletion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deletionDueAtUtc").GetDateTime();
        Assert.InRange(dueAt, DateTime.UtcNow.AddDays(14).AddMinutes(-1), DateTime.UtcNow.AddDays(14));

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(client, firstToken)).StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/candidate/auth/login", new { email, password = Password });
        var secondToken = await AccessTokenFromAsync(login);
        var pending = await ReadMeAsync(client, secondToken);
        Assert.Equal("PendingDeletion", pending.GetProperty("status").GetString());
        Assert.Equal(dueAt, pending.GetProperty("deletionDueAtUtc").GetDateTime());

        var cancel = new HttpRequestMessage(HttpMethod.Post, "/api/v1/candidate/account/cancel-deletion");
        cancel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(cancel)).StatusCode);

        var restored = await ReadMeAsync(client, secondToken);
        Assert.Equal("Active", restored.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, restored.GetProperty("deletionDueAtUtc").ValueKind);
    }

    [Fact]
    public async Task the_scheduled_job_should_erase_an_account_whose_grace_period_has_ended()
    {
        Guid accountId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CandidateAccountsDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<ICandidatePasswordHasher>();
            var account = CandidateAccount.Register(
                $"candidate-{Guid.NewGuid():N}@example.test", hasher.Hash(Password), "Mira", "Tolun",
                SupportedLanguages.Default);
            account.RequestDeletion(DateTime.UtcNow - CandidateAccount.DeletionGracePeriod);
            db.CandidateAccounts.Add(account);
            await db.SaveChangesAsync();
            accountId = account.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<CandidateAccountDeletionJob>().EraseDueAccountsAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CandidateAccountsDbContext>();
            var erased = await db.CandidateAccounts.IgnoreQueryFilters().SingleAsync(a => a.Id == accountId);
            Assert.Equal(CandidateAccountStatus.Deleted, erased.Status);
            Assert.Equal(CandidateAccount.AnonymizedFirstName, erased.FirstName);
        }
    }

    private static async Task<string> AccessTokenFromAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private static Task<HttpResponseMessage> GetMeAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/candidate/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadMeAsync(HttpClient client, string accessToken)
    {
        var response = await GetMeAsync(client, accessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // Own per-IP bucket: register, delete and login are rate limited.
    private HttpClient NewClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ExternalClientIpStartupFilter.ClientIpHeader, $"203.0.113.{Random.Shared.Next(1, 255)}");
        return client;
    }
}
