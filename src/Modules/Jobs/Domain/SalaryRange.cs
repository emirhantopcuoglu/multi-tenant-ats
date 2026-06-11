namespace Ats.Modules.Jobs.Domain;

public sealed record SalaryRange
{
    public decimal Min { get; }
    public decimal Max { get; }
    public string Currency { get; }

    public SalaryRange(decimal min, decimal max, string currency)
    {
        if (min < 0 || max < 0)
            throw new ArgumentException("Salary values cannot be negative.");
        if (max < min)
            throw new ArgumentException("Max salary cannot be less than min salary.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.");

        Min = min;
        Max = max;
        Currency = currency.ToUpperInvariant();
    }
}
