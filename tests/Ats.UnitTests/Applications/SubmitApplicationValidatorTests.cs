using Ats.Modules.Applications.Application.Applications;

namespace Ats.UnitTests.Applications;

// The validator is the one piece of the apply flow with no infrastructure dependency, so it is
// the natural unit-test target. The handler's DB/storage orchestration is covered by the
// integration tests added in Sprint 7 (Testcontainers).
public class SubmitApplicationValidatorTests
{
    private static SubmitApplicationCommand ValidCommand(
        string email = "jane@example.com", string firstName = "Jane", string lastName = "Doe") =>
        new(
            JobSlug: "senior-dev-1a2b3c",
            CandidateEmail: email,
            FirstName: firstName,
            LastName: lastName,
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
        // Arrange
        var command = ValidCommand();

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Rejects_a_missing_or_malformed_email(string email)
    {
        var result = _validator.Validate(ValidCommand(email: email));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SubmitApplicationCommand.CandidateEmail));
    }

    [Fact]
    public void Rejects_a_blank_first_name()
    {
        var result = _validator.Validate(ValidCommand(firstName: "  "));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SubmitApplicationCommand.FirstName));
    }

    [Fact]
    public void Rejects_a_blank_last_name()
    {
        var result = _validator.Validate(ValidCommand(lastName: ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SubmitApplicationCommand.LastName));
    }
}
