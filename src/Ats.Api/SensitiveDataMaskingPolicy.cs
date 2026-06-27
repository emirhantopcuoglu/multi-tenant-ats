using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Ats.Api;

// Applied when a log message uses the @ destructuring operator, e.g. Log.Information("{@Cmd}", cmd).
// Any property whose name matches a known sensitive key is replaced with "[REDACTED]" before the
// event is written to any sink, so passwords and tokens never reach Seq, the console, or files.
//
// The convention-based approach (trust named scalar parameters, avoid {@WholeObject} for sensitive
// types) is the primary defense; this policy is a safety net for cases where someone logs an object
// that happens to carry sensitive fields.
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
