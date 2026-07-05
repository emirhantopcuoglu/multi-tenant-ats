namespace Ats.Shared.Kernel;

// Port over CV parsing. The Application layer asks "extract structured data from this CV text, and
// judge it against this job's requirements" without knowing an LLM is behind it — the same
// Dependency Inversion as IFileStorage / IEmailSender. The concrete implementation
// (OpenAiCompatibleCvParser) lives in Infrastructure and owns the HTTP client, the prompt, and the
// resilience policy; callers see only this behaviour.
//
// The inputs are already-extracted plain text (see IPdfTextExtractor) and the job's free-text
// description, not raw files or entities: keeping the LLM port text-in / data-out means it has no
// opinion on file formats, storage, or the Jobs module's schema.
public interface ICvParser
{
    Task<CvParseResult> ParseAsync(
        string cvText, string jobDescription, CancellationToken cancellationToken = default);
}

// What we ask the model to pull out of a CV, plus its assessment of fit against the job it was
// submitted for. Unknown/empty values are represented as their empty form (empty list, 0, empty
// string) rather than nulls: it keeps the JSON schema we send to the model free of nullable unions
// (which structured-output schemas constrain) and the stored document uniformly shaped. Mapping
// "unknown" semantics is a presentation concern, not this port's.
//
// The fit fields are deliberately narrow: a qualitative rating (never a numeric score, which reads
// as false precision for an LLM's judgment) plus a short grounded reason, and requirement gaps
// limited to concrete technical/skill items from the job description — never inferences about
// career gaps, tenure patterns, or anything adjacent to a protected characteristic. The prompt
// enforces this; the shape here just has no field to carry that kind of content even if it tried.
public sealed record CvParseResult(
    IReadOnlyList<string> Skills,
    double TotalExperienceYears,
    IReadOnlyList<CvEducation> Education,
    IReadOnlyList<CvPosition> RecentPositions,
    CvJobFitRating JobFitRating,
    string FitSummary,
    IReadOnlyList<string> MatchedRequirements,
    IReadOnlyList<string> MissingRequirements);

public sealed record CvEducation(string Degree, string Institution, int Year);

public sealed record CvPosition(string Title, string Company, string StartDate, string EndDate);

// Qualitative only, on purpose (see CvParseResult) — Moderate is the safe default when the model's
// rating can't be confidently parsed, rather than defaulting to either extreme.
public enum CvJobFitRating
{
    Weak,
    Moderate,
    Strong
}
