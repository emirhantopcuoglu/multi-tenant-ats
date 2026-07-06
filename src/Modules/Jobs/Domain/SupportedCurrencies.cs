namespace Ats.Modules.Jobs.Domain;

// Kept as string constants rather than a C# enum: SalaryRange is an EF Core owned type whose
// constructor runs every time an existing Job is loaded, so binding Currency to an enum via
// HasConversion<string>() would throw when reading a row whose stored value falls outside this
// list (e.g. a legacy free-text entry). The restriction below only applies at the input
// boundary (command validators + the frontend dropdown), never when reading existing data.
public static class SupportedCurrencies
{
    public const string TRY = "TRY";
    public const string USD = "USD";
    public const string EUR = "EUR";
    public const string GBP = "GBP";

    public static readonly IReadOnlyList<string> All = [TRY, USD, EUR, GBP];
}
