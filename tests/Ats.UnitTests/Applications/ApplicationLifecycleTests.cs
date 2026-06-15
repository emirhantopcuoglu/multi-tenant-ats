using Ats.Modules.Applications.Domain;

namespace Ats.UnitTests.Applications;

public class ApplicationLifecycleTests
{
    private static Application CreateActive() =>
        Application.Create(
            jobId: Guid.NewGuid(), candidateId: Guid.NewGuid(), initialStageId: Guid.NewGuid(),
            cvFileKey: "tenant/app/cv.pdf", coverLetter: null);

    [Fact]
    public void Create_should_start_active_in_the_initial_stage()
    {
        var initialStage = Guid.NewGuid();

        var application = Application.Create(Guid.NewGuid(), Guid.NewGuid(), initialStage, "k.pdf");

        Assert.Equal(ApplicationStatus.Active, application.Status);
        Assert.Equal(initialStage, application.CurrentStageId);
        Assert.NotEqual(default, application.AppliedAtUtc);
    }

    [Fact]
    public void Create_should_throw_when_cv_file_key_is_missing()
    {
        var act = () => Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "   ");

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void MoveToStage_should_update_current_stage_while_active()
    {
        var application = CreateActive();
        var nextStage = Guid.NewGuid();

        application.MoveToStage(nextStage);

        Assert.Equal(nextStage, application.CurrentStageId);
        Assert.Equal(ApplicationStatus.Active, application.Status);
    }

    [Fact]
    public void Reject_should_set_status_and_keep_the_reason()
    {
        var application = CreateActive();

        application.Reject("Not enough experience");

        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        Assert.Equal("Not enough experience", application.RejectionReason);
    }

    [Fact]
    public void Reject_should_throw_when_reason_is_blank()
    {
        var application = CreateActive();

        Assert.Throws<ArgumentException>(() => application.Reject(" "));
    }

    [Fact]
    public void Hire_should_set_status_to_hired()
    {
        var application = CreateActive();

        application.Hire();

        Assert.Equal(ApplicationStatus.Hired, application.Status);
    }

    [Fact]
    public void Withdraw_should_set_status_to_withdrawn()
    {
        var application = CreateActive();

        application.Withdraw();

        Assert.Equal(ApplicationStatus.Withdrawn, application.Status);
    }

    [Fact]
    public void A_terminal_application_cannot_be_moved_or_decided_again()
    {
        var application = CreateActive();
        application.Hire();

        Assert.Throws<InvalidOperationException>(() => application.MoveToStage(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => application.Reject("x"));
        Assert.Throws<InvalidOperationException>(() => application.Withdraw());
    }
}
