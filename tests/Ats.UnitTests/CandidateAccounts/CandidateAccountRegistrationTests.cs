using Ats.Modules.CandidateAccounts.Domain;

namespace Ats.UnitTests.CandidateAccounts;

public class CandidateAccountRegistrationTests
{
    private const string PasswordHash = "hashed-password";

    [Fact]
    public void Register_should_normalize_the_email_to_lowercase()
    {
        var account = CandidateAccount.Register("Jane@Example.COM", PasswordHash, "Jane", "Doe");

        Assert.Equal("jane@example.com", account.Email);
    }

    [Fact]
    public void Register_should_trim_surrounding_whitespace_from_names_and_email()
    {
        var account = CandidateAccount.Register("  jane@example.com  ", PasswordHash, "  Jane  ", "  Doe  ");

        Assert.Equal("jane@example.com", account.Email);
        Assert.Equal("Jane", account.FirstName);
        Assert.Equal("Doe", account.LastName);
    }

    [Fact]
    public void Register_should_store_the_supplied_password_hash_verbatim()
    {
        // The domain never hashes: it persists whatever the infrastructure layer already computed.
        var account = CandidateAccount.Register("jane@example.com", PasswordHash, "Jane", "Doe");

        Assert.Equal(PasswordHash, account.PasswordHash);
    }

    [Fact]
    public void Register_should_start_with_no_cv_attached()
    {
        var account = CandidateAccount.Register("jane@example.com", PasswordHash, "Jane", "Doe");

        Assert.Null(account.CvFileKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Register_should_reject_a_missing_email(string? email)
    {
        Assert.Throws<ArgumentException>(
            () => CandidateAccount.Register(email!, PasswordHash, "Jane", "Doe"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Register_should_reject_a_missing_password_hash(string? passwordHash)
    {
        Assert.Throws<ArgumentException>(
            () => CandidateAccount.Register("jane@example.com", passwordHash!, "Jane", "Doe"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Register_should_reject_a_missing_first_name(string? firstName)
    {
        Assert.Throws<ArgumentException>(
            () => CandidateAccount.Register("jane@example.com", PasswordHash, firstName!, "Doe"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Register_should_reject_a_missing_last_name(string? lastName)
    {
        Assert.Throws<ArgumentException>(
            () => CandidateAccount.Register("jane@example.com", PasswordHash, "Jane", lastName!));
    }

}
