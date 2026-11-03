using System.Text.Json;
using Ats.Shared.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.UnitTests.Shared;

public sealed class ConcurrencyExceptionHandlerTests
{
    [Fact]
    public async Task should_answer_409_with_a_conflict_code_when_a_save_hits_a_concurrent_change()
    {
        // Arrange
        var handler = new ConcurrencyExceptionHandler(NullLogger<ConcurrencyExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        var handled = await handler.TryHandleAsync(
            context, new DbUpdateConcurrencyException("stale"), CancellationToken.None);

        // Assert: the frontend reads `code`, so it can tell a conflict from a crash
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("concurrency_conflict", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task should_leave_other_exceptions_to_the_next_handler()
    {
        var handler = new ConcurrencyExceptionHandler(NullLogger<ConcurrencyExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            new DefaultHttpContext(), new InvalidOperationException(), CancellationToken.None);

        Assert.False(handled);
    }
}
