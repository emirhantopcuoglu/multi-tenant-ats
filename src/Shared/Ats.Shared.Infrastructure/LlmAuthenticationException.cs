namespace Ats.Shared.Infrastructure;

/// <summary>
/// The LLM provider rejected the credentials (401/403). Its own type so retries skip it: the
/// same key will be rejected every time.
/// </summary>
public sealed class LlmAuthenticationException : Exception
{
    public LlmAuthenticationException(string message) : base(message)
    {
    }
}
