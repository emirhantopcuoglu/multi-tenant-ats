using Ats.Shared.Kernel;

namespace Ats.Modules.Tenants.Application;

public static class UserManagementErrors
{
    // Also returned for users in another tenant, so ids from other tenants aren't revealed.
    public static readonly Error NotFound =
        new("user_management.not_found", "User not found.");

    public static readonly Error InvalidRole =
        new("user_management.invalid_role", "Unknown role.");

    // Separate errors because the fix differs: promote someone else first, or ask a colleague.
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
