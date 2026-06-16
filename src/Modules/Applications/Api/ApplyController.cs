using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Applications.Api;

// Public, unauthenticated application endpoint. The tenant comes from the path slug, resolved
// by TenantResolutionMiddleware before the request reaches here. No api/v{version} prefix:
// this is a candidate-facing URL like /acmecorp/jobs/senior-dev-1a2b3c/apply.
[ApiController]
[AllowAnonymous]
[Route("{slug}/jobs/{jobSlug}/apply")]
public sealed class ApplyController : ControllerBase
{
    // The CV ceiling is enforced here at the boundary, before the file is streamed anywhere.
    private const long MaxCvSizeBytes = 10 * 1024 * 1024;

    // Enough leading bytes to cover every whitelisted signature (PDF/DOCX are 4 bytes).
    private const int SignatureProbeBytes = 8;

    private readonly ISender _sender;
    private readonly ICurrentTenant _currentTenant;

    public ApplyController(ISender sender, ICurrentTenant currentTenant)
    {
        _sender = sender;
        _currentTenant = currentTenant;
    }

    [HttpPost]
    [RequestSizeLimit(MaxCvSizeBytes + 1024 * 1024)] // file + multipart/form-field overhead
    public async Task<IActionResult> Apply(
        string slug, string jobSlug, [FromForm] ApplyForm form, CancellationToken ct)
    {
        // An unknown company slug leaves the tenant unresolved. Surface 404 rather than a
        // misleading validation error — the company page genuinely does not exist.
        if (!_currentTenant.TenantId.HasValue)
            return NotFound();

        if (form.Cv is null || form.Cv.Length == 0)
            return BadRequest(ToProblem(FileSignatureValidator.Empty));

        // Validate the real file at the boundary: read its leading bytes and check them against
        // the whitelist. Never trust the extension or the client's declared content type.
        await using var cvStream = form.Cv.OpenReadStream();
        var header = new byte[SignatureProbeBytes];
        var bytesRead = await cvStream.ReadAsync(header, ct);

        var validation = FileSignatureValidator.Validate(
            header.AsSpan(0, bytesRead), form.Cv.ContentType, form.Cv.Length, MaxCvSizeBytes);
        if (validation.IsFailure)
            return BadRequest(ToProblem(validation.Error));

        // Rewind so the handler streams the whole file from the start into storage.
        cvStream.Position = 0;

        var command = new SubmitApplicationCommand(
            jobSlug, form.Email, form.FirstName, form.LastName, form.Phone, form.LinkedInUrl,
            form.CoverLetter, cvStream, form.Cv.Length, form.Cv.ContentType, form.Cv.FileName);

        var result = await _sender.Send(command, ct);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new { id = result.Value })
            : MapFailure(result.Error);
    }

    // Translates a domain error code into the right HTTP status. Keeping this mapping in the
    // controller leaves the handler free of transport concerns.
    private IActionResult MapFailure(Error error) => error.Code switch
    {
        "application.job_not_available" => NotFound(ToProblem(error)),
        "application.tenant_not_resolved" => NotFound(ToProblem(error)),
        "application.duplicate" => Conflict(ToProblem(error)),
        _ => BadRequest(ToProblem(error))
    };

    private static object ToProblem(Error error) => new { error.Code, error.Message };

    // Bound from multipart/form-data. IFormFile carries the CV; the rest are plain form fields.
    public sealed class ApplyForm
    {
        public string Email { get; init; } = null!;
        public string FirstName { get; init; } = null!;
        public string LastName { get; init; } = null!;
        public string? Phone { get; init; }
        public string? LinkedInUrl { get; init; }
        public string? CoverLetter { get; init; }
        public IFormFile? Cv { get; init; }
    }
}
