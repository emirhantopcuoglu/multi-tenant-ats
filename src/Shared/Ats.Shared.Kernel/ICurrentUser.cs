namespace Ats.Shared.Kernel;

// The id of the authenticated user for the current request, or null when there is
// no authenticated user (anonymous endpoints, background work). Used to stamp the
// "who" of audit fields. Mirrors ICurrentTenant for the tenant dimension.
public interface ICurrentUser
{
    Guid? UserId { get; }
}
