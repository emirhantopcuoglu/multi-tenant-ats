namespace Ats.Modules.Jobs.Domain;

// Strings, not an enum: SalaryRange is loaded through its constructor, so an enum conversion
// would throw on old free-text values. The list is only enforced on input.
public static class SupportedCurrencies
{
    public const string TRY = "TRY";
    public const string USD = "USD";
    public const string EUR = "EUR";
    public const string GBP = "GBP";

    public static readonly IReadOnlyList<string> All = [TRY, USD, EUR, GBP];
}
