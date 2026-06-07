namespace Ats.Modules.Tenants.Domain;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public TenantStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Tenant(Guid id, string name, string slug, TenantStatus status, DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    private Tenant() { }

    public static Tenant Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Tenant slug is required.", nameof(slug));

        return new Tenant(Guid.NewGuid(), name, slug.ToLowerInvariant(), TenantStatus.Trial, DateTime.UtcNow);
    }

    public void Activate() => Status = TenantStatus.Active;

    public void Suspend()
    {
        if (Status == TenantStatus.Suspended)
            throw new InvalidOperationException("Tenant is already suspended.");
        Status = TenantStatus.Suspended;
    }
}

public enum TenantStatus
{
    Trial,
    Active,
    Suspended
}