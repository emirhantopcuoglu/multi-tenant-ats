using Ats.Modules.Applications.Application.Applications;

namespace Ats.UnitTests.Applications;

// The command validators are the infrastructure-free part of the recruiter flow. The handlers'
// DB orchestration (stage-belongs-to-pipeline, terminal-state guards) is covered by the
// integration tests added later with Testcontainers.
public class ApplicationCommandValidatorTests
{
    [Fact]
    public void Reject_requires_a_reason()
    {
        var validator = new RejectApplicationValidator();

        var result = validator.Validate(new RejectApplicationCommand(Guid.NewGuid(), "  "));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RejectApplicationCommand.Reason));
    }

    [Fact]
    public void Reject_passes_with_an_application_id_and_reason()
    {
        var validator = new RejectApplicationValidator();

        var result = validator.Validate(
            new RejectApplicationCommand(Guid.NewGuid(), "Not enough experience"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void MoveStage_requires_both_ids()
    {
        var validator = new MoveApplicationStageValidator();

        var result = validator.Validate(new MoveApplicationStageCommand(Guid.Empty, Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(MoveApplicationStageCommand.ApplicationId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(MoveApplicationStageCommand.TargetStageId));
    }
}
