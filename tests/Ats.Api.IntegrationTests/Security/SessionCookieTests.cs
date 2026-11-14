using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Tenants.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Ats.Api.IntegrationTests.Security;

// The refresh token lives only in an HttpOnly cookie, so a script on the page can never read it.
[Collection(ApiTestCollection.Name)]
public sealed class SessionCookieTests
{
    private const string CompanyCookie = "ats_refresh";
    private const string CompanyPath = "/api/v1/auth";
    private const string CandidateCookie = "ats_candidate_refresh";
    private const string CandidatePath = "/api/v1/candidate/auth";
    private const string Password = "Correct-Horse-9";

    private readonly AtsApiFactory _factory;

    public SessionCookieTests(AtsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task a_company_session_should_live_only_in_the_cookie_from_login_to_logout()
    {
        var client = NewClient();
        var email = await RegisterConfirmedCompanyUserAsync(client);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        var (refreshToken, _) = await AssertSessionAsync(login, CompanyCookie, CompanyPath);

        var refresh = await SendWithCookieAsync(client, "/api/v1/auth/refresh", CompanyCookie, refreshToken);
        var (rotated, _) = await AssertSessionAsync(refresh, CompanyCookie, CompanyPath);
        Assert.NotEqual(refreshToken, rotated);

        var logout = await SendWithCookieAsync(client, "/api/v1/auth/logout", CompanyCookie, rotated);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        AssertCleared(logout, CompanyCookie, CompanyPath);

        var afterLogout = await SendWithCookieAsync(client, "/api/v1/auth/refresh", CompanyCookie, rotated);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task a_candidate_session_should_live_only_in_the_cookie_through_a_password_change()
    {
        var client = NewClient();
        var register = await client.PostAsJsonAsync("/api/v1/candidate/auth/register", new
        {
            email = $"candidate-{Guid.NewGuid():N}@example.test",
            password = Password,
            firstName = "Ada",
            lastName = "Yilmaz",
        });
        var (refreshToken, _) = await AssertSessionAsync(register, CandidateCookie, CandidatePath);

        var refresh = await SendWithCookieAsync(client, "/api/v1/candidate/auth/refresh", CandidateCookie, refreshToken);
        var (rotated, accessToken) = await AssertSessionAsync(refresh, CandidateCookie, CandidatePath);

        var change = new HttpRequestMessage(HttpMethod.Post, "/api/v1/candidate/profile/password")
        {
            Content = JsonContent.Create(new { currentPassword = Password, newPassword = "Other-Horse-10" }),
        };
        change.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var (afterChange, _) = await AssertSessionAsync(await client.SendAsync(change), CandidateCookie, CandidatePath);
        Assert.NotEqual(rotated, afterChange);

        var logout = await SendWithCookieAsync(client, "/api/v1/candidate/auth/logout", CandidateCookie, afterChange);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        AssertCleared(logout, CandidateCookie, CandidatePath);
    }

    [Theory]
    [InlineData("/api/v1/auth/refresh")]
    [InlineData("/api/v1/candidate/auth/refresh")]
    public async Task a_refresh_without_the_cookie_should_be_refused(string url)
    {
        var response = await NewClient().PostAsync(url, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Body has the access token only; the cookie carries the refresh token with every protection.
    private static async Task<(string RefreshToken, string AccessToken)> AssertSessionAsync(
        HttpResponseMessage response, string name, string path)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));
        Assert.False(body.TryGetProperty("refreshToken", out _));

        var cookie = SingleCookie(response, name);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal(path, cookie.Path.Value);
        Assert.InRange(cookie.Expires!.Value, DateTimeOffset.UtcNow.AddDays(6), DateTimeOffset.UtcNow.AddDays(8));
        return (cookie.Value.Value!, accessToken!);
    }

    private static void AssertCleared(HttpResponseMessage response, string name, string path)
    {
        var cookie = SingleCookie(response, name);
        Assert.Equal(path, cookie.Path.Value);
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
    }

    private static SetCookieHeaderValue SingleCookie(HttpResponseMessage response, string name) =>
        Assert.Single(
            SetCookieHeaderValue.ParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList()),
            cookie => cookie.Name.Value == name);

    private static Task<HttpResponseMessage> SendWithCookieAsync(
        HttpClient client, string url, string name, string value)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add(HeaderNames.Cookie, $"{name}={value}");
        return client.SendAsync(request);
    }

    // Own per-IP bucket, so the login rate limit can't be shared with other tests. Cookies are sent
    // by hand, so the test sees exactly what travels.
    private HttpClient NewClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add(ExternalClientIpStartupFilter.ClientIpHeader, ExternalClientIpStartupFilter.NextClientIp());
        return client;
    }

    private async Task<string> RegisterConfirmedCompanyUserAsync(HttpClient client)
    {
        var email = $"owner-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            companyName = "Lumora Studio",
            slug = $"lumora-{Guid.NewGuid():N}"[..20],
            email,
            password = Password,
            firstName = "Deniz",
            lastName = "Kaya",
        });
        register.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        await users.ConfirmEmailAsync(user, await users.GenerateEmailConfirmationTokenAsync(user));
        return email;
    }
}
