namespace Ats.Shared.Contracts.Jobs;

// The Jobs module's public read surface for other modules. Modules never reference each
// other directly (a data leak and a coupling risk); they talk through ports like this one,
// which lives in a neutral shared assembly both sides can see. The Applications module uses
// it to confirm a job exists and is open before accepting an application, without ever
// touching the Jobs schema or its EF model.
//
// Tenant scoping is implicit: the implementation runs inside the resolved tenant's context,
// so the global query filter already restricts the lookup to the current tenant.
public interface IJobDirectory
{
    Task<PublishedJob?> GetPublishedJobBySlugAsync(string slug, CancellationToken cancellationToken = default);
}

// A minimal read model — only what a consumer needs to attach an application to a job. It is
// deliberately not the Job entity: exposing the aggregate would leak the Jobs module's
// internals and let other modules depend on its shape.
public sealed record PublishedJob(Guid Id, string Title, string Slug);
