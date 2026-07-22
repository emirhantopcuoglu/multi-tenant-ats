using Ats.Modules.Applications.Domain;

namespace Ats.UnitTests.Applications;

public class PipelineTests
{
    [Fact]
    public void CreateDefault_should_build_the_standard_six_stage_funnel_in_order()
    {
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());

        var orderedNames = pipeline.Stages.OrderBy(s => s.Order).Select(s => s.Name);

        Assert.Equal(
            new[] { "Applied", "Screening", "Interview", "Offer", "Hired", "Rejected" },
            orderedNames);
    }

    [Fact]
    public void CreateDefault_should_mark_exactly_one_initial_and_two_terminal_stages()
    {
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());

        Assert.Single(pipeline.Stages, s => s.Type == PipelineStageType.Initial);
        Assert.Single(pipeline.Stages, s => s.Type == PipelineStageType.FinalHired);
        Assert.Single(pipeline.Stages, s => s.Type == PipelineStageType.FinalRejected);
    }

    [Fact]
    public void CreateDefault_should_mark_the_interview_stage_with_the_interview_type()
    {
        // The auto-advance-on-interview-scheduled consumer finds this stage by Type, not by
        // matching the user-facing Name, so the funnel must carry the distinct Interview type.
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());

        var interviewStage = Assert.Single(pipeline.Stages, s => s.Name == "Interview");
        Assert.Equal(PipelineStageType.Interview, interviewStage.Type);
    }

    [Fact]
    public void InitialStage_should_be_the_applied_stage()
    {
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());

        Assert.Equal("Applied", pipeline.InitialStage.Name);
    }

    [Fact]
    public void CreateDefault_should_attach_every_stage_to_the_pipeline()
    {
        var pipeline = Pipeline.CreateDefault(Guid.NewGuid());

        Assert.All(pipeline.Stages, s => Assert.Equal(pipeline.Id, s.PipelineId));
    }

    [Fact]
    public void CreateDefault_should_throw_when_job_id_is_empty()
    {
        Assert.Throws<ArgumentException>(() => Pipeline.CreateDefault(Guid.Empty));
    }
}
