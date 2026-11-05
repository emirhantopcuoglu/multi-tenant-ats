using System.Net;
using System.Net.Http.Json;
using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Tenants.Domain;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.Security;

// Walks every controller endpoint the app actually maps, so a new endpoint is covered without a new test.
[Collection(ApiTestCollection.Name)]
public sealed class EndpointAuthorizationTests
{
    private static readonly string[] CompanyRoles = [Roles.Admin, Roles.Recruiter, Roles.HiringManager, Roles.ReadOnly];

    // The spec, written out on purpose instead of read from the app's policy setup.
    private static readonly Dictionary<string, string[]> RolesByPolicy = new()
    {
        [Policies.CanManageJobs] = [Roles.Admin, Roles.Recruiter],
        [Policies.CanViewJobs] = CompanyRoles,
        [Policies.CanManageUsers] = [Roles.Admin],
        [Policies.CanManageTenant] = [Roles.Admin],
        [Policies.CanViewApplications] = CompanyRoles,
        [Policies.CanManageApplications] = [Roles.Admin, Roles.Recruiter],
        [Policies.CanViewInterviews] = CompanyRoles,
        [Policies.CanManageInterviews] = [Roles.Admin, Roles.Recruiter, Roles.HiringManager],
        [Policies.CandidateOnly] = []
    };

    // Every endpoint and the rule that guards it, so changing one shows up in review. "signed-in" lets a
    // candidate token through too: those are tenant-scoped or keyed by the caller's own id, and the
    // interview room serves both on purpose.
    private static readonly string[] ApprovedAuthorizationSurface =
    [
        "DELETE /api/v{version:apiVersion}/candidate/account -> CandidateOnly",
        "DELETE /api/v{version:apiVersion}/candidate/profile/cv -> CandidateOnly",
        "GET /api/v{version:apiVersion}/applications -> CanViewApplications",
        "GET /api/v{version:apiVersion}/applications/{id:guid} -> CanViewApplications",
        "GET /api/v{version:apiVersion}/applications/{id:guid}/activities -> CanViewApplications",
        "GET /api/v{version:apiVersion}/applications/{id:guid}/cv-download-url -> CanViewApplications",
        "GET /api/v{version:apiVersion}/applications/{id:guid}/cv-parse-result -> CanViewApplications",
        "GET /api/v{version:apiVersion}/auth/me -> signed-in",
        "GET /api/v{version:apiVersion}/candidate/applications -> CandidateOnly",
        "GET /api/v{version:apiVersion}/candidate/applications/job-ids -> CandidateOnly",
        "GET /api/v{version:apiVersion}/candidate/applications/{id:guid} -> CandidateOnly",
        "GET /api/v{version:apiVersion}/candidate/auth/me -> CandidateOnly",
        "GET /api/v{version:apiVersion}/candidate/interviews -> CandidateOnly",
        "GET /api/v{version:apiVersion}/candidate/notifications -> CandidateOnly",
        "GET /api/v{version:apiVersion}/candidate/notifications/unread-count -> CandidateOnly",
        "GET /api/v{version:apiVersion}/candidate/profile -> CandidateOnly",
        "GET /api/v{version:apiVersion}/candidate/profile/cv/download-url -> CandidateOnly",
        "GET /api/v{version:apiVersion}/candidates -> CanViewApplications",
        "GET /api/v{version:apiVersion}/dashboard/stats -> signed-in",
        "GET /api/v{version:apiVersion}/interview-room/{roomToken} -> signed-in",
        "GET /api/v{version:apiVersion}/interviews -> CanViewInterviews",
        "GET /api/v{version:apiVersion}/interviews/outcome -> CanViewInterviews",
        "GET /api/v{version:apiVersion}/interviews/{id:guid} -> CanViewInterviews",
        "GET /api/v{version:apiVersion}/interviews/{id:guid}/feedback -> CanViewInterviews",
        "GET /api/v{version:apiVersion}/jobs -> CanViewJobs",
        "GET /api/v{version:apiVersion}/jobs/{id:guid} -> CanViewJobs",
        "GET /api/v{version:apiVersion}/jobs/{jobId:guid}/stages -> CanViewApplications",
        "GET /api/v{version:apiVersion}/notifications -> signed-in",
        "GET /api/v{version:apiVersion}/notifications/unread-count -> signed-in",
        "GET /api/v{version:apiVersion}/tenant/profile -> CanManageTenant",
        "GET /api/v{version:apiVersion}/users -> signed-in",
        "GET /public/companies -> public",
        "GET /public/companies/{slug} -> public",
        "GET /public/jobs -> public",
        "GET /{slug}/jobs -> public",
        "GET /{slug}/jobs/{jobSlug} -> public",
        "POST /api/v{version:apiVersion}/applications/{id:guid}/correct-stage -> CanManageApplications",
        "POST /api/v{version:apiVersion}/applications/{id:guid}/hire -> CanManageApplications",
        "POST /api/v{version:apiVersion}/applications/{id:guid}/move-stage -> CanManageApplications",
        "POST /api/v{version:apiVersion}/applications/{id:guid}/reject -> CanManageApplications",
        "POST /api/v{version:apiVersion}/auth/confirm-email -> public",
        "POST /api/v{version:apiVersion}/auth/forgot-password -> public",
        "POST /api/v{version:apiVersion}/auth/login -> public",
        "POST /api/v{version:apiVersion}/auth/logout -> public",
        "POST /api/v{version:apiVersion}/auth/refresh -> public",
        "POST /api/v{version:apiVersion}/auth/register -> public",
        "POST /api/v{version:apiVersion}/auth/resend-confirmation -> public",
        "POST /api/v{version:apiVersion}/auth/reset-password -> public",
        "POST /api/v{version:apiVersion}/candidate/account/freeze -> CandidateOnly",
        "POST /api/v{version:apiVersion}/candidate/account/reactivate -> CandidateOnly",
        "POST /api/v{version:apiVersion}/candidate/applications/{id:guid}/withdraw -> CandidateOnly",
        "POST /api/v{version:apiVersion}/candidate/auth/forgot-password -> public",
        "POST /api/v{version:apiVersion}/candidate/auth/login -> public",
        "POST /api/v{version:apiVersion}/candidate/auth/logout -> public",
        "POST /api/v{version:apiVersion}/candidate/auth/refresh -> public",
        "POST /api/v{version:apiVersion}/candidate/auth/register -> public",
        "POST /api/v{version:apiVersion}/candidate/auth/resend-verification -> CandidateOnly",
        "POST /api/v{version:apiVersion}/candidate/auth/reset-password -> public",
        "POST /api/v{version:apiVersion}/candidate/auth/verify-email -> public",
        "POST /api/v{version:apiVersion}/candidate/notifications/read-all -> CandidateOnly",
        "POST /api/v{version:apiVersion}/candidate/notifications/{id:guid}/read -> CandidateOnly",
        "POST /api/v{version:apiVersion}/candidate/profile/cv -> CandidateOnly",
        "POST /api/v{version:apiVersion}/candidate/profile/email -> CandidateOnly",
        "POST /api/v{version:apiVersion}/candidate/profile/email/confirm -> public",
        "POST /api/v{version:apiVersion}/candidate/profile/password -> CandidateOnly",
        "POST /api/v{version:apiVersion}/interviews -> CanManageInterviews",
        "POST /api/v{version:apiVersion}/interviews/{id:guid}/cancel -> CanManageInterviews",
        "POST /api/v{version:apiVersion}/interviews/{id:guid}/complete -> CanManageInterviews",
        "POST /api/v{version:apiVersion}/interviews/{id:guid}/feedback -> CanViewInterviews",
        "POST /api/v{version:apiVersion}/interviews/{id:guid}/no-show -> CanManageInterviews",
        "POST /api/v{version:apiVersion}/invitations -> CanManageUsers",
        "POST /api/v{version:apiVersion}/invitations/accept -> public",
        "POST /api/v{version:apiVersion}/jobs -> CanManageJobs",
        "POST /api/v{version:apiVersion}/jobs/{id:guid}/archive -> CanManageJobs",
        "POST /api/v{version:apiVersion}/jobs/{id:guid}/close -> CanManageJobs",
        "POST /api/v{version:apiVersion}/jobs/{id:guid}/publish -> CanManageJobs",
        "POST /api/v{version:apiVersion}/notifications/read-all -> signed-in",
        "POST /api/v{version:apiVersion}/notifications/{id:guid}/read -> signed-in",
        "POST /api/v{version:apiVersion}/users/{id:guid}/deactivate -> CanManageUsers",
        "POST /api/v{version:apiVersion}/users/{id:guid}/reactivate -> CanManageUsers",
        "POST /{slug}/jobs/{jobSlug}/apply -> CandidateOnly",
        "PUT /api/v{version:apiVersion}/auth/me/language -> signed-in",
        "PUT /api/v{version:apiVersion}/candidate/profile -> CandidateOnly",
        "PUT /api/v{version:apiVersion}/candidate/profile/language -> CandidateOnly",
        "PUT /api/v{version:apiVersion}/interviews/{id:guid}/interviewers -> CanManageInterviews",
        "PUT /api/v{version:apiVersion}/interviews/{id:guid}/reschedule -> CanManageInterviews",
        "PUT /api/v{version:apiVersion}/jobs/{id:guid} -> CanManageJobs",
        "PUT /api/v{version:apiVersion}/tenant/profile -> CanManageTenant",
        "PUT /api/v{version:apiVersion}/users/{id:guid}/role -> CanManageUsers"
    ];

    private static int _nextClientIp;

    private readonly AtsApiFactory _factory;

    public EndpointAuthorizationTests(AtsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void every_endpoint_should_keep_its_approved_rule()
    {
        AssertMatches(ApprovedAuthorizationSurface, ControllerEndpoints().Select(e => $"{Describe(e)} -> {RuleOf(e)}"));
    }

    [Fact]
    public async Task every_protected_endpoint_should_answer_401_without_a_token()
    {
        var client = _factory.CreateClient();
        var wrong = new List<string>();

        foreach (var endpoint in ControllerEndpoints().Where(e => !IsPublic(e)))
        {
            var status = await SendAsync(client, endpoint);
            if (status != HttpStatusCode.Unauthorized)
                wrong.Add($"{Describe(endpoint)} -> {(int)status}");
        }

        AssertNone(wrong);
    }

    [Fact]
    public async Task each_role_should_be_let_through_only_where_its_policy_allows()
    {
        var wrong = new List<string>();

        foreach (var role in CompanyRoles)
        {
            var client = _factory.CreateCompanyClient(role, Guid.NewGuid());
            foreach (var endpoint in ControllerEndpoints().Where(e => !IsPublic(e) && PoliciesOf(e).Length > 0))
            {
                var isAllowed = PoliciesOf(endpoint).All(p => RolesByPolicy[p].Contains(role));
                var status = await SendAsync(client, endpoint);
                if ((status == HttpStatusCode.Forbidden) == isAllowed)
                    wrong.Add($"{role}: {Describe(endpoint)} -> {(int)status}");
            }
        }

        AssertNone(wrong);
    }

    [Fact]
    public async Task a_candidate_token_from_before_a_password_change_should_get_401_everywhere()
    {
        var (client, accountId) = await _factory.CreateCandidateClientAsync();
        await _factory.ChangeCandidatePasswordAsync(accountId);
        var wrong = new List<string>();

        foreach (var endpoint in ControllerEndpoints().Where(e => !IsPublic(e)))
        {
            var status = await SendAsync(client, endpoint);
            if (status != HttpStatusCode.Unauthorized)
                wrong.Add($"{Describe(endpoint)} -> {(int)status}");
        }

        AssertNone(wrong);
    }

    [Fact]
    public async Task a_current_candidate_token_should_get_past_auth_on_every_candidate_endpoint()
    {
        var (client, _) = await _factory.CreateCandidateClientAsync();
        var wrong = new List<string>();

        foreach (var endpoint in ControllerEndpoints().Where(e => PoliciesOf(e).Contains(Policies.CandidateOnly)))
        {
            var status = await SendAsync(client, endpoint);
            if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                wrong.Add($"{Describe(endpoint)} -> {(int)status}");
        }

        AssertNone(wrong);
    }

    [Fact]
    public async Task a_candidate_token_should_be_refused_by_every_company_policy()
    {
        var (client, _) = await _factory.CreateCandidateClientAsync();
        var wrong = new List<string>();

        foreach (var endpoint in ControllerEndpoints()
            .Where(e => !IsPublic(e) && PoliciesOf(e).Length > 0 && !PoliciesOf(e).Contains(Policies.CandidateOnly)))
        {
            var status = await SendAsync(client, endpoint);
            if (status != HttpStatusCode.Forbidden)
                wrong.Add($"{Describe(endpoint)} -> {(int)status}");
        }

        AssertNone(wrong);
    }

    private static void AssertMatches(string[] approved, IEnumerable<string> actual)
    {
        var found = actual.ToHashSet();
        var unapproved = found.Except(approved).Order().Select(e => $"not approved: {e}");
        var missing = approved.Except(found).Order().Select(e => $"no longer exists: {e}");
        AssertNone([.. unapproved, .. missing]);
    }

    private static void AssertNone(List<string> wrong) =>
        Assert.True(wrong.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, wrong));

    private IEnumerable<RouteEndpoint> ControllerEndpoints() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null);

    private static bool IsPublic(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
        || endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0;

    private static string RuleOf(RouteEndpoint endpoint) =>
        IsPublic(endpoint) ? "public"
        : PoliciesOf(endpoint) is { Length: > 0 } policies ? string.Join(" + ", policies.Order())
        : "signed-in";

    private static string[] PoliciesOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(a => a.Policy)
            .OfType<string>()
            .Distinct()
            .ToArray();

    private static string MethodOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.Single();

    private static string Describe(RouteEndpoint endpoint) =>
        $"{MethodOf(endpoint)} /{endpoint.RoutePattern.RawText}";

    private static async Task<HttpStatusCode> SendAsync(HttpClient client, RouteEndpoint endpoint)
    {
        var method = new HttpMethod(MethodOf(endpoint));
        using var request = new HttpRequestMessage(method, UrlFor(endpoint.RoutePattern));
        request.Headers.Add(ExternalClientIpStartupFilter.ClientIpHeader, NextClientIp());
        if (method != HttpMethod.Get && method != HttpMethod.Delete)
            request.Content = AcceptsForm(endpoint) ? new MultipartFormDataContent() : JsonContent.Create(new { });

        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    // Routing answers 415 before authorization runs if the body type doesn't match.
    private static bool AcceptsForm(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes.Any(t => t.StartsWith("multipart/")) == true;

    private static string NextClientIp()
    {
        var n = Interlocked.Increment(ref _nextClientIp);
        return $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}";
    }

    // Fills route parameters with values their constraints accept, so routing reaches the endpoint.
    private static string UrlFor(RoutePattern pattern) =>
        "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternParameterPart { Name: "version" } => "1.0",
            RoutePatternParameterPart parameter when parameter.ParameterPolicies.Any(p => p.Content == "guid")
                => Guid.NewGuid().ToString(),
            RoutePatternParameterPart parameter when parameter.ParameterPolicies.Any(p => p.Content == "int") => "1",
            RoutePatternParameterPart => "sample",
            _ => string.Empty
        }))));
}
