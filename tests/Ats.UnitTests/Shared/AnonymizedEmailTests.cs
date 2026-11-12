using Ats.Shared.Kernel;

namespace Ats.UnitTests.Shared;

public class AnonymizedEmailTests
{
    [Fact]
    public void Should_be_unique_per_id_and_undeliverable()
    {
        var first = AnonymizedEmail.For(Guid.NewGuid());
        var second = AnonymizedEmail.For(Guid.NewGuid());

        Assert.NotEqual(first, second);
        // ".invalid" is reserved by RFC 2606, the placeholder can never receive real mail.
        Assert.EndsWith("@account.invalid", first);
    }
}
