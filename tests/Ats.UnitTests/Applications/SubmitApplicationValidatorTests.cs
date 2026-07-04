using Ats.Modules.Applications.Application.Applications;

namespace Ats.UnitTests.Applications;

// The validator is the one piece of the apply flow with no infrastructure dependency, so it is
// the natural unit-test target. The handler's DB/storage orchestration is covered by the
// integration tests added in Sprint 7 (Testcontainers).
public class SubmitApplicationValidatorTests
{
    private static SubmitApplicationCommand ValidCommand(
        Guid? candidateAccountId = null) =>
        new(
            JobSlug: "senior-dev-1a2b3c",
            CandidateAccountId: candidateAccountId ?? Guid.NewGuid(),
            Phone: null,
            LinkedInUrl: null,
            CoverLetter: null,
            CvContent: Stream.Null,
            CvSizeBytes: 1024,
            CvContentType: "application/pdf",
            CvFileName: "cv.pdf");

    private readonly SubmitApplicationValidator _validator = new();

    [Fact]
    public void Valid_command_passes()
    {
        var result = _validator.Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rejects_an_empty_candidate_account_id()
    {
        var result = _validator.Validate(ValidCommand(candidateAccountId: Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            e => e.PropertyName == nameof(SubmitApplicationCommand.CandidateAccountId));
    }

    [Fact]
    public void Rejects_a_phone_that_exceeds_40_characters()
    {
        var command = ValidCommand() with { Phone = new string('9', 41) };
        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SubmitApplicationCommand.Phone));
    }

    [Theory]
    [InlineData("+90 (555) 111-22-33")]
    [InlineData("0555.111.2233")]
    [InlineData("5551112233")]
    public void Accepts_common_phone_formats(string phone)
    {
        var result = _validator.Validate(ValidCommand() with { Phone = phone });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("call me maybe")]  // letters
    [InlineData("12345")]          // too few digits
    [InlineData("+1234567890123456")] // 16 digits, beyond E.164
    public void Rejects_an_implausible_phone(string phone)
    {
        var result = _validator.Validate(ValidCommand() with { Phone = phone });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SubmitApplicationCommand.Phone));
    }

    [Theory]
    [InlineData("https://linkedin.com/in/someone")]
    [InlineData("http://example.com/profile")]
    public void Accepts_an_absolute_http_linkedin_url(string url)
    {
        var result = _validator.Validate(ValidCommand() with { LinkedInUrl = url });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("linkedin.com/in/someone")] // relative
    [InlineData("ftp://linkedin.com/in/x")] // wrong scheme
    [InlineData("not a url")]
    public void Rejects_a_linkedin_value_that_is_not_an_http_url(string url)
    {
        var result = _validator.Validate(ValidCommand() with { LinkedInUrl = url });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            e => e.PropertyName == nameof(SubmitApplicationCommand.LinkedInUrl));
    }

    [Fact]
    public void Rejects_a_cover_letter_that_exceeds_5000_characters()
    {
        var command = ValidCommand() with { CoverLetter = new string('a', 5001) };
        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SubmitApplicationCommand.CoverLetter));
    }
}
