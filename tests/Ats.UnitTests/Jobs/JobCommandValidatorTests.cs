using Ats.Modules.Jobs.Application.Jobs;
using Ats.Modules.Jobs.Domain;

namespace Ats.UnitTests.Jobs;

// Covers the salary-currency and country/city allow-lists added to Create/Update: the same closed
// lists the frontend dropdowns offer must be the only values the API accepts.
public class JobCommandValidatorTests
{
    [Fact]
    public void CreateJob_rejects_a_currency_outside_the_supported_list()
    {
        var validator = new CreateJobValidator();

        var result = validator.Validate(new CreateJobCommand(
            "Staff Engineer", "Lead the platform team", "Engineering", "Istanbul", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Lead, WorkArrangement.Remote,
            120000m, 160000m, "CAD", Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateJobCommand.SalaryCurrency));
    }

    [Fact]
    public void CreateJob_accepts_a_supported_currency_regardless_of_case()
    {
        var validator = new CreateJobValidator();

        var result = validator.Validate(new CreateJobCommand(
            "Staff Engineer", "Lead the platform team", "Engineering", "Istanbul", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Lead, WorkArrangement.Remote,
            120000m, 160000m, "usd", Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateJob_does_not_require_a_currency_when_no_salary_is_set()
    {
        var validator = new CreateJobValidator();

        var result = validator.Validate(new CreateJobCommand(
            "Recruiter", "Run hiring pipelines", "People", "Istanbul", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Mid, WorkArrangement.OnSite,
            null, null, null, Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateJob_rejects_a_country_outside_the_supported_list()
    {
        var validator = new CreateJobValidator();

        var result = validator.Validate(new CreateJobCommand(
            "Recruiter", "Run hiring pipelines", "People", "Toronto", "Canada",
            EmploymentType.FullTime, ExperienceLevel.Mid, WorkArrangement.OnSite,
            null, null, null, Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateJobCommand.Country));
    }

    [Fact]
    public void CreateJob_rejects_a_city_that_does_not_belong_to_the_selected_country()
    {
        var validator = new CreateJobValidator();

        // Paris is a real city in our list, just not one that belongs to Turkey.
        var result = validator.Validate(new CreateJobCommand(
            "Recruiter", "Run hiring pipelines", "People", "Paris", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Mid, WorkArrangement.OnSite,
            null, null, null, Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateJobCommand.City));
    }

    [Fact]
    public void UpdateJob_rejects_a_currency_outside_the_supported_list()
    {
        var validator = new UpdateJobValidator();

        var result = validator.Validate(new UpdateJobCommand(
            Guid.NewGuid(), "Staff Engineer", "Lead the platform team", "Engineering", "Istanbul", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Lead, WorkArrangement.Remote,
            120000m, 160000m, "Turkish Lira"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateJobCommand.SalaryCurrency));
    }

    [Fact]
    public void UpdateJob_accepts_a_supported_currency()
    {
        var validator = new UpdateJobValidator();

        var result = validator.Validate(new UpdateJobCommand(
            Guid.NewGuid(), "Staff Engineer", "Lead the platform team", "Engineering", "Istanbul", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Lead, WorkArrangement.Remote,
            120000m, 160000m, "EUR"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateJob_rejects_a_city_that_does_not_belong_to_the_selected_country()
    {
        var validator = new UpdateJobValidator();

        var result = validator.Validate(new UpdateJobCommand(
            Guid.NewGuid(), "Staff Engineer", "Lead the platform team", "Engineering", "Berlin", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Lead, WorkArrangement.Remote,
            null, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateJobCommand.City));
    }
}
