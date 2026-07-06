using Ats.Modules.Jobs.Domain;

namespace Ats.UnitTests.Jobs;

public class JobLifecycleTests
{
    private static Job CreateDraft() =>
        Job.Create(
            "Senior Developer", "Build things", "Engineering", "Remote", country: null,
            EmploymentType.FullTime, ExperienceLevel.Senior, WorkArrangement.Remote,
            salaryRange: null, createdBy: Guid.NewGuid());

    [Fact]
    public void Create_should_start_in_draft_status()
    {
        var job = CreateDraft();

        Assert.Equal(JobStatus.Draft, job.Status);
    }

    [Fact]
    public void Create_should_throw_when_title_is_blank()
    {
        var act = () => Job.Create(
            "   ", "desc", "Eng", "Remote", null,
            EmploymentType.FullTime, ExperienceLevel.Senior, WorkArrangement.Remote, null, Guid.NewGuid());

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Publish_should_move_draft_to_published_and_stamp_time()
    {
        var job = CreateDraft();

        job.Publish();

        Assert.Equal(JobStatus.Published, job.Status);
        Assert.NotNull(job.PublishedAtUtc);
    }

    [Fact]
    public void Publish_should_throw_when_job_is_not_draft()
    {
        var job = CreateDraft();
        job.Publish();

        Assert.Throws<InvalidOperationException>(() => job.Publish());
    }

    [Fact]
    public void Close_should_move_published_to_closed()
    {
        var job = CreateDraft();
        job.Publish();

        job.Close();

        Assert.Equal(JobStatus.Closed, job.Status);
        Assert.NotNull(job.ClosedAtUtc);
    }

    [Fact]
    public void Close_should_throw_when_job_is_not_published()
    {
        var job = CreateDraft();

        Assert.Throws<InvalidOperationException>(() => job.Close());
    }

    [Fact]
    public void Archive_should_set_archived_status()
    {
        var job = CreateDraft();

        job.Archive();

        Assert.Equal(JobStatus.Archived, job.Status);
    }

    [Fact]
    public void Archive_should_throw_when_already_archived()
    {
        var job = CreateDraft();
        job.Archive();

        Assert.Throws<InvalidOperationException>(() => job.Archive());
    }

    [Fact]
    public void UpdateDetails_should_throw_when_job_is_archived()
    {
        var job = CreateDraft();
        job.Archive();

        var act = () => job.UpdateDetails(
            "New title", "desc", "Eng", "Remote", null,
            EmploymentType.FullTime, ExperienceLevel.Senior, WorkArrangement.Remote, null);

        Assert.Throws<InvalidOperationException>(act);
    }
}
