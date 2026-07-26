using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public static class UserManagementErrors
{
    // Covers "no such id" and "belongs to another tenant" alike: an Admin has no business learning
    // that a given id exists somewhere else on the platform.
    public static readonly Error NotFound =
        new("user_management.not_found", "User not found.");

    public static readonly Error InvalidRole =
        new("user_management.invalid_role", "Unknown role.");

    // Guards against the two ways an Admin can lock their own company out of its workspace. Separate
    // errors because the fix differs: promote someone else first, versus ask a colleague to do it.
    public static readonly Error LastAdmin =
        new("user_management.last_admin",
            "This is the only active administrator. Promote another administrator first.");

    public static readonly Error CannotTargetSelf =
        new("user_management.cannot_target_self",
            "You cannot change your own role or deactivate your own account.");

    public static readonly Error AlreadyInThatState =
        new("user_management.already_in_that_state", "The user is already in that state.");

    public static readonly Error TenantNotResolved =
        new("user_management.tenant_not_resolved", "No tenant in scope for this request.");
}
