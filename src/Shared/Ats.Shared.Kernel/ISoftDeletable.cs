namespace Ats.Shared.Kernel;

// Marks an entity that is never physically removed. A delete is intercepted and
// turned into a flag update; a global query filter then hides flagged rows from
// every read unless explicitly opted out with IgnoreQueryFilters().
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTime? DeletedAtUtc { get; }
    Guid? DeletedBy { get; }
}
