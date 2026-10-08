using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.CandidateAccounts;

// The contract worth pinning here is the return value: it is the only thing that tells the caller
// which object in storage is now unreferenced. Get it wrong and either a live CV is deleted or an
// orphaned one is kept forever — both invisible from the database alone.
public class CandidateAccountCvTests
{
    private const string PasswordHash = "hashed-password";
    private const string FirstKey = "candidates/1/first-cv.pdf";
    private const string SecondKey = "candidates/1/second-cv.pdf";

    private static CandidateAccount CreateAccount() =>
        CandidateAccount.Register("jane@example.com", PasswordHash, "Jane", "Doe", SupportedLanguages.Default);

    [Fact]
    public void A_new_account_should_have_no_cv()
    {
        var account = CreateAccount();

        Assert.False(account.HasCv);
        Assert.Null(account.CvFileKey);
        Assert.Null(account.CvFileName);
        Assert.Null(account.CvUploadedAtUtc);
    }

    [Fact]
    public void Attaching_a_first_cv_should_displace_nothing()
    {
        var account = CreateAccount();

        var replacedKey = account.AttachCv(FirstKey, "cv.pdf");

        Assert.Null(replacedKey);
        Assert.True(account.HasCv);
        Assert.Equal(FirstKey, account.CvFileKey);
        Assert.Equal("cv.pdf", account.CvFileName);
        Assert.NotNull(account.CvUploadedAtUtc);
    }

    [Fact]
    public void Replacing_a_cv_should_return_the_previous_key_for_deletion()
    {
        var account = CreateAccount();
        account.AttachCv(FirstKey, "old.pdf");

        var replacedKey = account.AttachCv(SecondKey, "new.pdf");

        Assert.Equal(FirstKey, replacedKey);
        Assert.Equal(SecondKey, account.CvFileKey);
        Assert.Equal("new.pdf", account.CvFileName);
    }

    [Fact]
    public void Reattaching_the_same_key_should_displace_nothing()
    {
        // Otherwise the caller would delete the object it just attached.
        var account = CreateAccount();
        account.AttachCv(FirstKey, "cv.pdf");

        var replacedKey = account.AttachCv(FirstKey, "cv.pdf");

        Assert.Null(replacedKey);
        Assert.Equal(FirstKey, account.CvFileKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Attaching_should_reject_a_blank_key_or_name(string blank)
    {
        var account = CreateAccount();

        Assert.Throws<ArgumentException>(() => account.AttachCv(blank, "cv.pdf"));
        Assert.Throws<ArgumentException>(() => account.AttachCv(FirstKey, blank));
    }

    [Fact]
    public void Removing_a_cv_should_return_the_key_and_clear_every_field()
    {
        var account = CreateAccount();
        account.AttachCv(FirstKey, "cv.pdf");

        var removedKey = account.RemoveCv();

        Assert.Equal(FirstKey, removedKey);
        Assert.False(account.HasCv);
        Assert.Null(account.CvFileName);
        Assert.Null(account.CvUploadedAtUtc);
    }

    [Fact]
    public void Removing_a_cv_that_is_not_there_should_be_a_no_op()
    {
        var account = CreateAccount();

        var removedKey = account.RemoveCv();

        Assert.Null(removedKey);
        Assert.False(account.HasCv);
    }

    [Fact]
    public void Deleting_the_account_should_erase_every_cv_field()
    {
        // Erasure has to reach the file name too: it is the candidate's own text and would
        // otherwise survive the deletion in a column nobody thinks to look at.
        var account = CreateAccount();
        account.AttachCv(FirstKey, "jane-doe-cv.pdf");

        account.Delete();

        Assert.Null(account.CvFileKey);
        Assert.Null(account.CvFileName);
        Assert.Null(account.CvUploadedAtUtc);
    }
}
