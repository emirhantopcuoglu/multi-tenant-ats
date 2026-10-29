namespace Ats.Shared.Kernel;

// Takes extracted text and the job description, not files or entities.
public interface ICvParser
{
    Task<CvParseResult> ParseAsync(
        string cvText, string jobDescription, CancellationToken cancellationToken = default);
}

// Unknown values use empty forms (empty list, 0, "") instead of nulls. The fit fields are narrow
// on purpose: a qualitative rating, a short reason, and only concrete skill gaps. There is no
// field for career gaps or anything near a protected characteristic.
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

public enum CvJobFitRating
{
    Weak,
    Moderate,
    Strong
}
