using Serilog.Core;
using Serilog.Events;

namespace Ats.Api;

// Masks a logged value whose name says it is an email or a phone number. A safety net for the line
// that forgets the rule; it goes by the name, so {Mail} or {Recipient} would still get through.
public sealed class PersonalDataMaskingEnricher : ILogEventEnricher
{
    private static readonly string[] PersonalNameParts = ["Email", "Phone"];
    private static readonly ScalarValue Mask = new("[REDACTED]");

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var personal = logEvent.Properties
            .Where(property => property.Value is ScalarValue { Value: string } && IsPersonal(property.Key))
            .Select(property => property.Key)
            .ToList();

        foreach (var name in personal)
            logEvent.AddOrUpdateProperty(new LogEventProperty(name, Mask));
    }

    private static bool IsPersonal(string propertyName) =>
        PersonalNameParts.Any(part => propertyName.Contains(part, StringComparison.OrdinalIgnoreCase));
}
