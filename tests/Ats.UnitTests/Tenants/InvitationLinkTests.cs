using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;

namespace Ats.UnitTests.Tenants;

// Guards the one rule that ties the mailed invitation link to the SPA route it has to land on.
// The link is built from configuration and the route lives in TypeScript, so nothing else in the
// build can notice when the two drift apart — which is exactly how the default came to point at
// "/accept-invite" while the SPA served "/accept-invitation".
public class InvitationLinkTests
{
    [Fact]
    public void The_accept_link_path_must_be_a_reserved_slug()
    {
        var path = new Uri(new InvitationOptions().AcceptBaseUrl).AbsolutePath.Trim('/');

        // SlugPolicy reserves every bare top-level SPA route so a tenant slug can never shadow one.
        // The accept page is such a route, so its path must be reserved. If this passes as a valid
        // slug instead, the two have diverged: either the mailed link is dead, or the page is
        // registrable as a company slug and a tenant could shadow it.
        Assert.True(SlugPolicy.Validate(path).IsFailure);
    }
}
