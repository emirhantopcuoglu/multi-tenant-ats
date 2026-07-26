using Ats.Modules.Tenants.Domain;

namespace Ats.UnitTests.Tenants;

public class SlugPolicyTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("acme-corp")]
    [InlineData("acme-corp-2")]
    [InlineData("a1")]
    public void Validate_should_accept_a_well_formed_slug(string slug)
    {
        var result = SlugPolicy.Validate(slug);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")] // shorter than MinLength
    [InlineData("Acme")] // uppercase
    [InlineData("acme corp")] // space
    [InlineData("acme_corp")] // underscore
    [InlineData("acme/corp")] // slash would break the public URL
    [InlineData("-acme")] // leading hyphen
    [InlineData("acme-")] // trailing hyphen
    [InlineData("acme--corp")] // double hyphen
    public void Validate_should_reject_a_malformed_slug(string slug)
    {
        var result = SlugPolicy.Validate(slug);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("jobs")]
    [InlineData("login")]
    [InlineData("settings")]
    [InlineData("api")]
    [InlineData("hangfire")]
    [InlineData("public")]
    // Recovery pages. A tenant that claimed one of these would shadow the page its own admins need to
    // get back into the product — and the mailed link would land on a careers page instead.
    [InlineData("forgot-password")]
    [InlineData("reset-password")]
    public void Validate_should_reject_a_reserved_route_prefix(string slug)
    {
        var result = SlugPolicy.Validate(slug);

        // Reserved slugs would shadow an application or API route, so they must not be registrable.
        Assert.True(result.IsFailure);
        Assert.Equal(SlugErrors.Reserved.Code, result.Error.Code);
    }

    [Fact]
    public void Validate_should_reject_a_slug_over_the_max_length()
    {
        var tooLong = new string('a', SlugPolicy.MaxLength + 1);

        var result = SlugPolicy.Validate(tooLong);

        Assert.True(result.IsFailure);
        Assert.Equal(SlugErrors.InvalidLength.Code, result.Error.Code);
    }
}
