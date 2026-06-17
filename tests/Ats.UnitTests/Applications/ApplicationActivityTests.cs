using Ats.Modules.Applications.Domain;

namespace Ats.UnitTests.Applications;

public class ApplicationActivityTests
{
    [Fact]
    public void Submitted_has_no_actor_and_carries_job_and_email()
    {
        var jobId = Guid.NewGuid();

        var activity = ApplicationActivity.Submitted(Guid.NewGuid(), jobId, "jane@example.com");

        Assert.Equal(ApplicationActivityType.Submitted, activity.ActivityType);
        Assert.Null(activity.ActorUserId);
        Assert.Contains(jobId.ToString(), activity.Payload);
        Assert.Contains("jane@example.com", activity.Payload);
        Assert.NotEqual(default, activity.OccurredAtUtc);
    }

    [Fact]
    public void StageChanged_records_actor_and_both_stages()
    {
        var actor = Guid.NewGuid();
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        var activity = ApplicationActivity.StageChanged(Guid.NewGuid(), actor, from, to);

        Assert.Equal(ApplicationActivityType.StageChanged, activity.ActivityType);
        Assert.Equal(actor, activity.ActorUserId);
        Assert.Contains(from.ToString(), activity.Payload);
        Assert.Contains(to.ToString(), activity.Payload);
    }

    [Fact]
    public void Rejected_records_the_reason()
    {
        var activity = ApplicationActivity.Rejected(Guid.NewGuid(), Guid.NewGuid(), "Position filled");

        Assert.Equal(ApplicationActivityType.Rejected, activity.ActivityType);
        Assert.Contains("Position filled", activity.Payload);
    }
}
