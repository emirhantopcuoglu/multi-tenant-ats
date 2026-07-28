using Ats.Modules.Applications.Application.Applications;

namespace Ats.UnitTests.Applications;

// The handler used to re-clamp page/pageSize after the validator had already rejected the same
// values, so the clamp was unreachable and untestable. With it gone the validator is the only thing
// standing between a caller and an unbounded query, which is what these tests pin down.
public class SearchCandidatesValidatorTests
{
    private readonly SearchCandidatesValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_search_term(string term)
    {
        var result = _validator.Validate(new SearchCandidatesQuery(term));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchCandidatesQuery.Q));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_page_below_the_first_one(int page)
    {
        var result = _validator.Validate(new SearchCandidatesQuery("nadia", page));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchCandidatesQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void Rejects_a_page_size_outside_the_supported_range(int pageSize)
    {
        var result = _validator.Validate(new SearchCandidatesQuery("nadia", 1, pageSize));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchCandidatesQuery.PageSize));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Accepts_the_page_size_bounds_themselves(int pageSize)
    {
        // The bounds are inclusive; an off-by-one here would reject the page size the UI actually sends.
        var result = _validator.Validate(new SearchCandidatesQuery("nadia", 1, pageSize));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Accepts_the_defaults_the_controller_binds()
    {
        var result = _validator.Validate(new SearchCandidatesQuery("nadia"));

        Assert.True(result.IsValid);
    }
}
