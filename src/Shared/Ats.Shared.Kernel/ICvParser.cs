namespace Ats.Shared.Kernel;

// Port over CV parsing. The Application layer asks "extract structured data from this CV text"
// without knowing an LLM is behind it — the same Dependency Inversion as IFileStorage / IEmailSender.
// The concrete implementation (ClaudeCvParser) lives in Infrastructure and owns the Anthropic SDK,
// the prompt, and the resilience policy; callers see only this behaviour.
//
// The input is already-extracted plain text (see IPdfTextExtractor), not the raw file: keeping the
// LLM port text-in / data-out means it has no opinion on file formats or storage.
public interface ICvParser
{
    Task<CvParseResult> ParseAsync(string cvText, CancellationToken cancellationToken = default);
}

// What we ask the model to pull out of a CV. Unknown values are represented as their empty form
// (empty list, 0, empty string) rather than nulls: it keeps the JSON schema we send to the model
// free of nullable unions (which structured-output schemas constrain) and the stored document
// uniformly shaped. Mapping "unknown" semantics is a presentation concern, not this port's.
public sealed record CvParseResult(
    IReadOnlyList<string> Skills,
    double TotalExperienceYears,
    IReadOnlyList<CvEducation> Education,
    IReadOnlyList<CvPosition> RecentPositions);

public sealed record CvEducation(string Degree, string Institution, int Year);

public sealed record CvPosition(string Title, string Company, string StartDate, string EndDate);
