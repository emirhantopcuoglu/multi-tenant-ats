using Ats.Modules.Applications.Domain;

namespace Ats.UnitTests.Applications;

public class ApplicationLifecycleTests
{
    private static Application CreateActive() =>
        Application.Create(
            jobId: Guid.NewGuid(), candidateId: Guid.NewGuid(), candidateAccountId: null,
            initialStageId: Guid.NewGuid(), cvFileKey: "tenant/app/cv.pdf", coverLetter: null);

    [Fact]
    public void Create_should_start_active_in_the_initial_stage()
    {
        var initialStage = Guid.NewGuid();

        var application = Application.Create(Guid.NewGuid(), Guid.NewGuid(), null, initialStage, "k.pdf");

        Assert.Equal(ApplicationStatus.Active, application.Status);
        Assert.Equal(initialStage, application.CurrentStageId);
        Assert.NotEqual(default, application.AppliedAtUtc);
    }

    [Fact]
    public void Create_should_throw_when_cv_file_key_is_missing()
    {
        var act = () => Application.Create(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), "   ");

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
    public void Reject_should_set_status_and_reason_and_move_to_the_rejected_stage()
    {
        var application = CreateActive();
        var rejectedStage = Guid.NewGuid();

        application.Reject("Not enough experience", rejectedStage);

        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        Assert.Equal("Not enough experience", application.RejectionReason);
        Assert.Equal(rejectedStage, application.CurrentStageId);
    }

    [Fact]
    public void MarkViewed_should_stamp_only_the_first_view()
    {
        var application = CreateActive();

        var firstCall = application.MarkViewed();
        var stampedAt = application.FirstViewedAtUtc;
        var secondCall = application.MarkViewed();

        Assert.True(firstCall);
        Assert.False(secondCall);
        Assert.NotNull(stampedAt);
        Assert.Equal(stampedAt, application.FirstViewedAtUtc);
    }

    [Fact]
    public void MarkViewed_should_still_stamp_a_terminal_application()
    {
        // A rejected application can still be opened; the candidate deserves the receipt.
        var application = CreateActive();
        application.Reject("Not a fit", Guid.NewGuid());

        Assert.True(application.MarkViewed());
        Assert.NotNull(application.FirstViewedAtUtc);
    }

    [Fact]
    public void MarkCvDownloaded_should_stamp_only_the_first_download()
    {
        var application = CreateActive();

        var firstCall = application.MarkCvDownloaded();
        var stampedAt = application.FirstCvDownloadedAtUtc;
        var secondCall = application.MarkCvDownloaded();

        Assert.True(firstCall);
        Assert.False(secondCall);
        Assert.NotNull(stampedAt);
        Assert.Equal(stampedAt, application.FirstCvDownloadedAtUtc);
    }

    [Fact]
    public void Reject_should_throw_when_reason_is_blank()
    {
        var application = CreateActive();

        Assert.Throws<ArgumentException>(() => application.Reject(" ", Guid.NewGuid()));
    }

    [Fact]
    public void Reject_should_throw_when_rejected_stage_is_empty()
    {
        var application = CreateActive();

        Assert.Throws<ArgumentException>(() => application.Reject("Not a fit", Guid.Empty));
    }

    [Fact]
    public void Hire_should_set_status_and_move_to_the_hired_stage()
    {
        var application = CreateActive();
        var hiredStage = Guid.NewGuid();

        application.Hire(hiredStage);

        Assert.Equal(ApplicationStatus.Hired, application.Status);
        Assert.Equal(hiredStage, application.CurrentStageId);
    }

    [Fact]
    public void Hire_should_throw_when_hired_stage_is_empty()
    {
        var application = CreateActive();

        Assert.Throws<ArgumentException>(() => application.Hire(Guid.Empty));
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
        application.Hire(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => application.MoveToStage(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => application.Reject("x", Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => application.Withdraw());
    }
}
