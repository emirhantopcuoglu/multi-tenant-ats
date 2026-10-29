using Ats.Modules.Applications.Domain;

namespace Ats.UnitTests.Applications;

// A returning candidate's phone and LinkedIn are updated; a blank field means "nothing new", not
// "clear it".
public class CandidateContactDetailsTests
{
    [Fact]
    public void A_new_phone_and_linkedin_should_replace_the_stored_ones()
    {
        var candidate = Candidate.Create(
            "ada@acme.test", "Ada", "Applicant", "+90 555 000 0000", "https://linkedin.com/in/old");

        candidate.UpdateContactDetails("+90 555 111 2222", "https://linkedin.com/in/new");

        Assert.Equal("+90 555 111 2222", candidate.Phone);
        Assert.Equal("https://linkedin.com/in/new", candidate.LinkedInUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_field_should_leave_the_stored_value_alone(string? blank)
    {
        // An empty optional field can't be told apart from a deliberate clear, and wiping a working number
        // is the worse mistake.
        var candidate = Candidate.Create(
            "ada@acme.test", "Ada", "Applicant", "+90 555 000 0000", "https://linkedin.com/in/ada");

        candidate.UpdateContactDetails(blank, blank);

        Assert.Equal("+90 555 000 0000", candidate.Phone);
        Assert.Equal("https://linkedin.com/in/ada", candidate.LinkedInUrl);
    }

    [Fact]
    public void A_first_value_should_fill_a_detail_that_was_never_set()
    {
        var candidate = Candidate.Create("ada@acme.test", "Ada", "Applicant");

        candidate.UpdateContactDetails("+90 555 111 2222", null);

        Assert.Equal("+90 555 111 2222", candidate.Phone);
        Assert.Null(candidate.LinkedInUrl);
    }

    [Fact]
    public void Surrounding_whitespace_should_be_trimmed_like_it_is_on_create()
    {
        var candidate = Candidate.Create("ada@acme.test", "Ada", "Applicant");

        candidate.UpdateContactDetails("  +90 555 111 2222  ", "  https://linkedin.com/in/ada  ");

        Assert.Equal("+90 555 111 2222", candidate.Phone);
        Assert.Equal("https://linkedin.com/in/ada", candidate.LinkedInUrl);
    }
}
