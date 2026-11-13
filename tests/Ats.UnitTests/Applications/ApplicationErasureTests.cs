using Ats.Modules.Applications.Domain;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.Applications;

public class ApplicationErasureTests
{
    private const string CvFileKey = "tenant/candidate/mira-tolun-cv.pdf";

    private static Application CreateWithdrawn()
    {
        var application = Application.Create(
            jobId: Guid.NewGuid(), candidateId: Guid.NewGuid(), candidateAccountId: Guid.NewGuid(),
            initialStageId: Guid.NewGuid(), cvFileKey: CvFileKey, coverLetter: "I would love to join.");
        application.Withdraw();
        return application;
    }

    [Fact]
    public void Erasing_a_candidate_should_leave_no_contact_details()
    {
        var candidate = Candidate.Create(
            "mira@example.test", "Mira", "Tolun", "+905321234567", "https://www.linkedin.com/in/mira-tolun");

        candidate.Erase();

        Assert.Equal(AnonymizedEmail.For(candidate.Id), candidate.Email);
        Assert.Equal(Candidate.AnonymizedFirstName, candidate.FirstName);
        Assert.Equal(Candidate.AnonymizedLastName, candidate.LastName);
        Assert.Null(candidate.Phone);
        Assert.Null(candidate.LinkedInUrl);
    }

    [Fact]
    public void Erasing_an_application_should_return_the_cv_key_and_clear_the_candidates_text()
    {
        var application = CreateWithdrawn();

        var removedKey = application.ErasePersonalData();

        Assert.Equal(CvFileKey, removedKey);
        Assert.Null(application.CvFileKey);
        Assert.Null(application.CoverLetter);
    }

    // The reason is the company's note about the candidate.
    [Fact]
    public void Erasing_a_rejected_application_should_clear_the_rejection_reason_but_keep_the_outcome()
    {
        var rejectedStage = Guid.NewGuid();
        var application = Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CvFileKey);
        application.Reject("Weak on distributed systems.", rejectedStage);

        application.ErasePersonalData();

        Assert.Null(application.RejectionReason);
        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        Assert.Equal(rejectedStage, application.CurrentStageId);
    }

    // A retry after a failed run must not hand back a key twice.
    [Fact]
    public void Erasing_an_application_twice_should_return_no_key_the_second_time()
    {
        var application = CreateWithdrawn();
        application.ErasePersonalData();

        Assert.Null(application.ErasePersonalData());
    }

    // The retention period for its personal data counts from here.
    [Fact]
    public void Closing_an_application_in_any_way_should_record_when()
    {
        var before = DateTime.UtcNow;
        var withdrawn = CreateWithdrawn();
        var rejected = Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CvFileKey);
        rejected.Reject("Weak on distributed systems.", Guid.NewGuid());
        var hired = Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CvFileKey);
        hired.Hire(Guid.NewGuid());

        Assert.All(
            new[] { withdrawn, rejected, hired },
            application => Assert.InRange(application.ClosedAtUtc!.Value, before, DateTime.UtcNow));
    }

    [Fact]
    public void An_open_application_should_have_no_closing_time()
    {
        var application = Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CvFileKey);
        application.MoveToStage(Guid.NewGuid());

        Assert.Null(application.ClosedAtUtc);
    }

    // Otherwise the pipeline would keep a live application nobody can read or contact.
    [Fact]
    public void Erasing_an_active_application_should_be_refused()
    {
        var application = Application.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CvFileKey);

        Assert.Throws<InvalidOperationException>(() => application.ErasePersonalData());
        Assert.Equal(CvFileKey, application.CvFileKey);
    }
}
