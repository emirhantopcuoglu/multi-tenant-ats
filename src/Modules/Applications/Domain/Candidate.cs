using Ats.Shared.Kernel;

namespace Ats.Modules.Applications.Domain;

// A person who has applied to at least one job in a tenant. Deduplicated by
// (TenantId, Email): the same person applying to a second job reuses this record rather
// than creating a duplicate. TenantId is stamped by the persistence interceptor on insert.
public sealed class Candidate : ITenantScoped, IAuditable, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string? LinkedInUrl { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private Candidate() { }

    private Candidate(Guid id, string email, string firstName, string lastName, string? phone, string? linkedInUrl)
    {
        Id = id;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        Phone = phone;
        LinkedInUrl = linkedInUrl;
    }

    public static Candidate Create(
        string email, string firstName, string lastName, string? phone = null, string? linkedInUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        return new Candidate(
            Guid.NewGuid(), NormalizeEmail(email), firstName.Trim(), lastName.Trim(),
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            string.IsNullOrWhiteSpace(linkedInUrl) ? null : linkedInUrl.Trim());
    }

    // Stored normalised so the (TenantId, Email) uniqueness check is case-insensitive
    // without relying on collation: "Jane@x.com" and "jane@x.com" are the same person.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
