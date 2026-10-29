using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Ats.Api;

// Masks sensitive property names when an object is logged with {@...}. A safety net; the main rule
// is not to log such objects at all.
public sealed class SensitiveDataMaskingPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordhash", "token", "refreshtoken", "accesstoken",
        "secret", "apikey", "api_key", "connectionstring", "credential"
    };

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        var properties = value.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToList();

        if (properties.All(p => !SensitiveNames.Contains(p.Name)))
        {
            result = default!;
            return false; // Nothing sensitive — let Serilog's default destructuring handle it.
        }

        var logProperties = properties.Select(p =>
        {
            LogEventPropertyValue propValue = SensitiveNames.Contains(p.Name)
                ? new ScalarValue("[REDACTED]")
                : propertyValueFactory.CreatePropertyValue(p.GetValue(value), true);
            return new LogEventProperty(p.Name, propValue);
        }).ToList();

        result = new StructureValue(logProperties);
        return true;
    }
}
