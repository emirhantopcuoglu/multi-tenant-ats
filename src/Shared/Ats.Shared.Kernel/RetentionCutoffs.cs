namespace Ats.Shared.Kernel;

// Records older than these go. ExpiredTokenBeforeUtc is compared with the expiry, not the creation time.
public sealed record RetentionCutoffs(
    DateTime AuditLogBeforeUtc,
    DateTime ReadNotificationBeforeUtc,
    DateTime ExpiredTokenBeforeUtc);
