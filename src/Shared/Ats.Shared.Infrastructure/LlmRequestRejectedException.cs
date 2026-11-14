namespace Ats.Shared.Infrastructure;

/// <summary>
/// The LLM provider refused the request itself (a 4xx other than 401, 403, 408 or 429), for
/// example as too large. Its own type so retries skip it: the same request is refused every time.
/// </summary>
public sealed class LlmRequestRejectedException : Exception
{
    public LlmRequestRejectedException(string message) : base(message)
    {
    }
}
