using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.CandidateAccounts;

public class CandidateAccountProfileTests
{
    private const string PasswordHash = "hashed-password";

    private static CandidateAccount CreateAccount() =>
        CandidateAccount.Register("jane@example.com", PasswordHash, "Jane", "Doe", SupportedLanguages.Default);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void UpdateProfile_should_trim_and_replace_the_names()
    {
        var account = CreateAccount();

        account.UpdateProfile("  Janet  ", "  Roe  ", null, null, null, null);

        Assert.Equal("Janet", account.FirstName);
        Assert.Equal("Roe", account.LastName);
    }

    [Fact]
    public void UpdateProfile_should_leave_the_email_untouched()
    {
        var account = CreateAccount();

        account.UpdateProfile("Janet", "Roe", null, null, null, null);

        Assert.Equal("jane@example.com", account.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateProfile_should_reject_a_missing_first_name(string? firstName)
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(
            () => account.UpdateProfile(firstName!, "Doe", null, null, null, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateProfile_should_reject_a_missing_last_name(string? lastName)
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(
            () => account.UpdateProfile("Jane", lastName!, null, null, null, null));
    }

    [Fact]
    public void UpdateProfile_should_store_the_optional_fields()
    {
        var account = CreateAccount();
        var birthDate = Today.AddYears(-30);

        account.UpdateProfile("Jane", "Doe", "+905321234567", "Turkey", "Istanbul", birthDate);

        Assert.Equal("+905321234567", account.PhoneNumber);
        Assert.Equal("Turkey", account.Country);
        Assert.Equal("Istanbul", account.City);
        Assert.Equal(birthDate, account.BirthDate);
    }

    [Fact]
    public void UpdateProfile_should_allow_clearing_the_optional_fields()
    {
        var account = CreateAccount();
        account.UpdateProfile("Jane", "Doe", "+905321234567", "Turkey", "Istanbul", Today.AddYears(-30));

        account.UpdateProfile("Jane", "Doe", null, null, null, null);

        Assert.Null(account.PhoneNumber);
        Assert.Null(account.Country);
        Assert.Null(account.City);
        Assert.Null(account.BirthDate);
    }

    [Theory]
    [InlineData("+90 (532) 123-45-67", "+905321234567")]
    [InlineData("0532 123 45 67", "05321234567")]
    public void UpdateProfile_should_normalize_phone_formatting_to_digits(string input, string expected)
    {
        var account = CreateAccount();

        account.UpdateProfile("Jane", "Doe", input, null, null, null);

        Assert.Equal(expected, account.PhoneNumber);
    }

    [Theory]
    [InlineData("not-a-phone")]
    [InlineData("123456")] // below the 7-digit minimum
    [InlineData("+1234567890123456")] // above the E.164 15-digit maximum
    public void UpdateProfile_should_reject_an_invalid_phone_number(string phoneNumber)
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(
            () => account.UpdateProfile("Jane", "Doe", phoneNumber, null, null, null));
    }

    [Fact]
    public void UpdateProfile_should_treat_a_whitespace_phone_as_not_provided()
    {
        var account = CreateAccount();

        account.UpdateProfile("Jane", "Doe", "   ", null, null, null);

        Assert.Null(account.PhoneNumber);
    }

    [Fact]
    public void UpdateProfile_should_reject_a_city_without_a_country()
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(
            () => account.UpdateProfile("Jane", "Doe", null, null, "Istanbul", null));
    }

    [Fact]
    public void UpdateProfile_should_reject_a_country_without_a_city()
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(
            () => account.UpdateProfile("Jane", "Doe", null, "Turkey", null, null));
    }

    [Fact]
    public void UpdateProfile_should_reject_a_birth_date_in_the_future()
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(
            () => account.UpdateProfile("Jane", "Doe", null, null, null, Today.AddDays(1)));
    }

    [Fact]
    public void UpdateProfile_should_reject_a_candidate_younger_than_the_minimum_age()
    {
        var account = CreateAccount();
        var tooYoung = Today.AddYears(-CandidateAccount.MinimumAgeYears).AddDays(1);

        Assert.Throws<ArgumentException>(
            () => account.UpdateProfile("Jane", "Doe", null, null, null, tooYoung));
    }

    [Fact]
    public void UpdateProfile_should_accept_a_candidate_exactly_at_the_minimum_age()
    {
        var account = CreateAccount();
        var exactlyMinimumAge = Today.AddYears(-CandidateAccount.MinimumAgeYears);

        account.UpdateProfile("Jane", "Doe", null, null, null, exactlyMinimumAge);

        Assert.Equal(exactlyMinimumAge, account.BirthDate);
    }

    [Fact]
    public void UpdateProfile_should_reject_a_birth_date_older_than_the_maximum_age()
    {
        var account = CreateAccount();
        var tooOld = Today.AddYears(-CandidateAccount.MaximumAgeYears).AddDays(-1);

        Assert.Throws<ArgumentException>(
            () => account.UpdateProfile("Jane", "Doe", null, null, null, tooOld));
    }

    [Fact]
    public void Register_should_start_with_an_empty_profile()
    {
        var account = CreateAccount();

        Assert.Null(account.PhoneNumber);
        Assert.Null(account.Country);
        Assert.Null(account.City);
        Assert.Null(account.BirthDate);
    }
}
