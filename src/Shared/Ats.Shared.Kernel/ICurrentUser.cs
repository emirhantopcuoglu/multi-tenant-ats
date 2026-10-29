namespace Ats.Shared.Kernel;

// Null when there is no authenticated user (anonymous endpoints, background work).
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
}
