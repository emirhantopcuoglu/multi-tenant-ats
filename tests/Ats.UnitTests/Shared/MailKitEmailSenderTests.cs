using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.UnitTests.Shared;

public class MailKitEmailSenderTests
{
    // A reminder queued just before the erasure can still be on its way.
    [Fact]
    public async Task Should_not_try_to_mail_an_erased_account()
    {
        // Nothing listens on this port, so a real attempt would throw.
        var sender = new MailKitEmailSender(
            Options.Create(new EmailOptions { Host = "127.0.0.1", Port = 1, FromAddress = "noreply@ats.test" }),
            NullLogger<MailKitEmailSender>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            sender.SendAsync(AnonymizedEmail.For(Guid.NewGuid()), "Interview reminder", "<p>See you soon.</p>"));

        Assert.Null(exception);
    }
}
