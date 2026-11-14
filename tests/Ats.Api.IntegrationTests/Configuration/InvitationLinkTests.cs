using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Ats.Api.IntegrationTests.Configuration;

// The mailed link comes from configuration and the route lives in TypeScript, so nothing else
// notices if they drift apart.
public sealed class InvitationLinkTests
{
    [Fact]
    public void the_accept_link_path_should_be_a_reserved_slug()
    {
        var acceptBaseUrl = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json")
            .Build()[$"{InvitationOptions.SectionName}:{nameof(InvitationOptions.AcceptBaseUrl)}"];

        var path = new Uri(acceptBaseUrl!).AbsolutePath.Trim('/');

        // The accept page is a top-level SPA route, so SlugPolicy must reserve it.
        Assert.True(SlugPolicy.Validate(path).IsFailure);
    }
}
