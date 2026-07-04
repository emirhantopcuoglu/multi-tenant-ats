namespace Ats.Modules.Tenants.Domain;

public sealed class Tenant
{
    // The public profile fields are free-form text; the caps exist to bound storage and rendering,
    // not to encode format rules (the URL shape of Website is validated at the API boundary).
    public const int DescriptionMaxLength = 2000;
    public const int WebsiteMaxLength = 300;
    public const int LocationMaxLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public TenantStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Public company profile, shown on the careers page. All optional: a tenant is fully
    // functional without them, and existing rows predate these columns.
    public string? Description { get; private set; }
    public string? Website { get; private set; }
    public string? Location { get; private set; }

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

    // Whitespace-only input collapses to null so "cleared" and "never set" are the same state —
    // the careers page hides a section on null, and a blank string would render an empty block.
    public void UpdateProfile(string? description, string? website, string? location)
    {
        Description = Normalize(description, DescriptionMaxLength, nameof(description));
        Website = Normalize(website, WebsiteMaxLength, nameof(website));
        Location = Normalize(location, LocationMaxLength, nameof(location));
    }

    private static string? Normalize(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"Value exceeds the {maxLength}-character limit.", paramName);

        return trimmed;
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