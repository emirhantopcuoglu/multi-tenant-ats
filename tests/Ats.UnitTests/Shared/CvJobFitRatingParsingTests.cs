using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.Shared;

public sealed class CvJobFitRatingParsingTests
{
    [Theory]
    [InlineData("Strong", CvJobFitRating.Strong)]
    [InlineData("strong", CvJobFitRating.Strong)]
    [InlineData("  STRONG  ", CvJobFitRating.Strong)]
    [InlineData("Weak", CvJobFitRating.Weak)]
    [InlineData("weak", CvJobFitRating.Weak)]
    [InlineData("Moderate", CvJobFitRating.Moderate)]
    public void ParseFitRating_should_recognize_the_three_expected_values_case_insensitively(
        string value, CvJobFitRating expected)
    {
        Assert.Equal(expected, OpenAiCompatibleCvParser.ParseFitRating(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Excellent")]
    [InlineData("85%")]
    public void ParseFitRating_should_default_to_moderate_for_anything_unrecognized(string? value)
    {
        // Moderate, not Strong or Weak: a rating the model didn't return cleanly must never be read
        // as an extreme in either direction.
        Assert.Equal(CvJobFitRating.Moderate, OpenAiCompatibleCvParser.ParseFitRating(value));
    }
}
