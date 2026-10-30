using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ats.Modules.Applications.Api;

// The tenant comes from the path slug. Email and name come from the candidate account, not the form.
[ApiController]
[Authorize(Policy = Policies.CandidateOnly)]
[Route("{slug}/jobs/{jobSlug}/apply")]
public sealed class ApplyController : ControllerBase
{
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
        // An unknown company slug is a 404.
        if (!_currentTenant.TenantId.HasValue)
            return NotFound();

        var candidateAccountId = _currentUser.UserId!.Value;

        // No file means "use the CV on my account". An attached but empty file is a broken upload and
        // is rejected. Stream.Null keeps a single disposal point.
        await using var cvStream = form.Cv?.OpenReadStream() ?? Stream.Null;

        CvUpload? cv = null;

        if (form.Cv is not null)
        {
            if (form.Cv.Length == 0)
                return BadRequest(ToProblem(FileSignatureValidator.Empty));

            // The file's leading bytes decide the format, not the extension or content type.
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

    private IActionResult MapFailure(Error error) => error.Code switch
    {
        "application.job_not_available" => NotFound(ToProblem(error)),
        "application.tenant_not_resolved" => NotFound(ToProblem(error)),
        "application.candidate_account_not_found" => NotFound(ToProblem(error)),
        "application.duplicate" => Conflict(ToProblem(error)),
        // 403, not 400: nothing is wrong with the form; the account isn't allowed to apply yet.
        "application.email_not_verified" or "application.account_frozen" =>
            StatusCode(StatusCodes.Status403Forbidden, ToProblem(error)),
        _ => BadRequest(ToProblem(error))
    };

    private static object ToProblem(Error error) => new { error.Code, error.Message };

    // Cv is optional: without it the account's CV is used.
    public sealed class ApplyForm
    {
        public string? Phone { get; init; }
        public string? LinkedInUrl { get; init; }
        public string? CoverLetter { get; init; }
        public IFormFile? Cv { get; init; }
    }
}
