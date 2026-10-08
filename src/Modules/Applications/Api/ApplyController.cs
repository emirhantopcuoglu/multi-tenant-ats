using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ats.Modules.Applications.Api;

// Candidate-facing apply endpoint. The tenant comes from the path slug, resolved by
// TenantResolutionMiddleware. No api/v{version} prefix: the URL is candidate-facing
// (/{slug}/jobs/{jobSlug}/apply). Requires a CandidateOnly token — identity fields
// (email, name) are taken from the account rather than the form.
[ApiController]
[Authorize(Policy = Policies.CandidateOnly)]
[Route("{slug}/jobs/{jobSlug}/apply")]
public sealed class ApplyController : ControllerBase
{
    // The CV ceiling is enforced here at the boundary, before the file is streamed anywhere.
    private const long MaxCvSizeBytes = 10 * 1024 * 1024;

    private readonly ISender _sender;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public ApplyController(ISender sender, ICurrentTenant currentTenant, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    [RequestSizeLimit(MaxCvSizeBytes + 1024 * 1024)] // file + multipart/form-field overhead
    public async Task<IActionResult> Apply(
        string slug, string jobSlug, [FromForm] ApplyForm form, CancellationToken ct)
    {
        // An unknown company slug leaves the tenant unresolved. Surface 404 rather than a
        // misleading validation error — the company page genuinely does not exist.
        if (!_currentTenant.TenantId.HasValue)
            return NotFound();

        // CandidateOnly policy guarantees authentication, so UserId is present.
        var candidateAccountId = _currentUser.UserId!.Value;

        // No file attached means "use the CV on my account"; the handler resolves that and fails
        // with application.cv_required if there is none. An attached but empty file is a different
        // thing — that is a broken upload, and saying so beats silently applying with a CV the
        // candidate did not mean to send.
        // Opened unconditionally so the stream has a single owner and a single disposal point;
        // Stream.Null stands in when nothing was attached and is safe to dispose.
        await using var cvStream = form.Cv?.OpenReadStream() ?? Stream.Null;

        CvUpload? cv = null;

        if (form.Cv is not null)
        {
            if (form.Cv.Length == 0)
                return BadRequest(ToProblem(FileSignatureValidator.Empty));

            // Validate the real file at the boundary: the leading bytes decide the format. Never
            // trust the extension or the client's declared content type.
            var validation = await FileSignatureValidator.ValidateAsync(
                cvStream, form.Cv.ContentType, form.Cv.Length, MaxCvSizeBytes, ct);
            if (validation.IsFailure)
                return BadRequest(ToProblem(validation.Error));

            cv = new CvUpload(cvStream, form.Cv.Length, form.Cv.ContentType, form.Cv.FileName);
        }

        var command = new SubmitApplicationCommand(
            jobSlug, candidateAccountId, form.Phone, form.LinkedInUrl, form.CoverLetter, cv);

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
        "application.candidate_account_not_found" => NotFound(ToProblem(error)),
        "application.duplicate" => Conflict(ToProblem(error)),
        // 403, not 400: the request is well-formed and the caller is authenticated — they are simply
        // not yet permitted to do this. A 400 would read as "you sent something wrong", which would
        // send the candidate looking for a mistake in the form instead of at their inbox.
        "application.email_not_verified" => StatusCode(StatusCodes.Status403Forbidden, ToProblem(error)),
        _ => BadRequest(ToProblem(error))
    };

    private static object ToProblem(Error error) => new { error.Code, error.Message };

    // Bound from multipart/form-data. Identity fields (email, name) come from the authenticated
    // CandidateAccount; only supplementary profile data is submitted at apply time. Cv is optional:
    // leaving it out reuses the CV stored on the account.
    public sealed class ApplyForm
    {
        public string? Phone { get; init; }
        public string? LinkedInUrl { get; init; }
        public string? CoverLetter { get; init; }
        public IFormFile? Cv { get; init; }
    }
}
