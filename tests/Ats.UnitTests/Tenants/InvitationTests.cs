using Ats.Modules.Tenants.Domain;

namespace Ats.UnitTests.Tenants;

public class InvitationTests
{
    private const string TokenHash = "hashed-token";

    [Fact]
    public void Create_should_lowercase_the_email()
    {
        var invitation = Invitation.Create("Recruiter@Acme.COM", Roles.Recruiter, TokenHash, validDays: 7);

        Assert.Equal("recruiter@acme.com", invitation.Email);
    }

    [Fact]
    public void IsValid_should_be_true_for_a_fresh_unaccepted_invitation()
    {
        var invitation = Invitation.Create("rec@acme.com", Roles.Recruiter, TokenHash, validDays: 7);

        Assert.True(invitation.IsValid);
    }

    [Fact]
    public void IsValid_should_be_false_when_expired()
    {
        // A negative validity window places the expiry in the past.
        var invitation = Invitation.Create("rec@acme.com", Roles.Recruiter, TokenHash, validDays: -1);

        Assert.False(invitation.IsValid);
    }

    [Fact]
    public void IsValid_should_be_false_after_being_accepted()
    {
        var invitation = Invitation.Create("rec@acme.com", Roles.Recruiter, TokenHash, validDays: 7);

        invitation.MarkAccepted();

        Assert.False(invitation.IsValid);
        Assert.NotNull(invitation.AcceptedAtUtc);
    }
}
