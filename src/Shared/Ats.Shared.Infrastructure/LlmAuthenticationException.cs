namespace Ats.Shared.Infrastructure;

/// <summary>
/// The LLM provider rejected our credentials (401/403).
/// </summary>
/// <remarks>
/// Its own type so the resilience pipeline can tell a configuration problem from a transient one.
/// Everything else the provider can do to us — a timeout, a 5xx, a dropped connection — is worth
/// another attempt. This is not: the same key will be rejected the same way for as long as it is
/// the key, and retrying only delays the message that would have said so.
/// </remarks>
public sealed class LlmAuthenticationException : Exception
{
    public LlmAuthenticationException(string message) : base(message)
    {
    }
}
