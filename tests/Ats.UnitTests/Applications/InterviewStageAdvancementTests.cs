using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;

namespace Ats.UnitTests.Applications;

public sealed class InterviewStageAdvancementTests
{
    [Fact]
    public void FindTarget_should_return_the_interview_stage_when_the_application_is_earlier_in_the_funnel()
    {
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());
        var applied = pipeline.Stages.Single(s => s.Name == "Applied");
        var interview = pipeline.Stages.Single(s => s.Name == "Interview");

        var target = InterviewStageAdvancement.FindTarget(ApplicationStatus.Active, applied.Id, pipeline.Stages);

        Assert.Equal(interview.Id, target?.Id);
    }

    [Fact]
    public void FindTarget_should_not_move_an_application_already_at_the_interview_stage()
    {
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());
        var interview = pipeline.Stages.Single(s => s.Name == "Interview");

        var target = InterviewStageAdvancement.FindTarget(ApplicationStatus.Active, interview.Id, pipeline.Stages);

        Assert.Null(target);
    }

    [Fact]
    public void FindTarget_should_never_pull_an_application_backwards_from_a_later_stage()
    {
        // A follow-up interview scheduled while the application already sits in Offer must not
        // regress it back to Interview.
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());
        var offer = pipeline.Stages.Single(s => s.Name == "Offer");

        var target = InterviewStageAdvancement.FindTarget(ApplicationStatus.Active, offer.Id, pipeline.Stages);

        Assert.Null(target);
    }

    [Theory]
    [InlineData(ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Hired)]
    public void FindTarget_should_do_nothing_for_a_non_active_application(ApplicationStatus status)
    {
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());
        var applied = pipeline.Stages.Single(s => s.Name == "Applied");

        var target = InterviewStageAdvancement.FindTarget(status, applied.Id, pipeline.Stages);

        Assert.Null(target);
    }

    [Fact]
    public void FindTarget_should_do_nothing_when_the_pipeline_has_no_interview_type_stage()
    {
        // A pipeline that predates the Interview type backfill (or a hypothetical custom pipeline
        // without one) must not throw — it simply has nothing to advance into.
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());
        var stagesWithoutInterviewType = pipeline.Stages
            .Where(s => s.Type != PipelineStageType.Interview)
            .ToList();
        var applied = stagesWithoutInterviewType.Single(s => s.Name == "Applied");

        var target = InterviewStageAdvancement.FindTarget(
            ApplicationStatus.Active, applied.Id, stagesWithoutInterviewType);

        Assert.Null(target);
    }
}
