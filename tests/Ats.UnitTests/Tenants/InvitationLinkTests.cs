using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;

namespace Ats.UnitTests.Tenants;

// The mailed link comes from configuration and the route lives in TypeScript, so nothing else
// notices if they drift apart.
public class InvitationLinkTests
{
    [Fact]
    public void The_accept_link_path_must_be_a_reserved_slug()
    {
        var path = new Uri(new InvitationOptions().AcceptBaseUrl).AbsolutePath.Trim('/');

        // The accept page is a top-level SPA route, so SlugPolicy must reserve it.
        Assert.True(SlugPolicy.Validate(path).IsFailure);
    }
}
