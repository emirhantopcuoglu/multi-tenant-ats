using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Application;

// Typed, structured failures returned via Result instead of thrown. The controller maps each
// code to an HTTP status, so the transport concern (404 vs 409) stays out of the handler.
public static class ApplicationErrors
{
    public static readonly Error TenantNotResolved =
        new("application.tenant_not_resolved", "The company could not be resolved from the URL.");

    public static readonly Error JobNotAvailable =
        new("application.job_not_available", "This job does not exist or is not open for applications.");

    public static readonly Error DuplicateApplication =
        new("application.duplicate", "An active application for this job already exists.");
}
