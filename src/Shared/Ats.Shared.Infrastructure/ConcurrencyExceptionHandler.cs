using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Shared.Infrastructure;

// A save that lost a race to a concurrent change becomes a 409, not a 500. Runs before
// GlobalExceptionHandler.
public sealed class ConcurrencyExceptionHandler : IExceptionHandler
{
    private const string ConflictCode = "concurrency_conflict";

    private readonly ILogger<ConcurrencyExceptionHandler> _logger;

    public ConcurrencyExceptionHandler(ILogger<ConcurrencyExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException concurrencyException)
            return false;

        _logger.LogWarning(
            "Concurrent update rejected for {Path} on {Entities}",
            httpContext.Request.Path,
            concurrencyException.Entries.Select(entry => entry.Metadata.ClrType.Name));

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "This record was changed by someone else. Reload and try again.",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        };
        problem.Extensions["code"] = ConflictCode;

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
