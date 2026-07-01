namespace Ats.Modules.CandidateAccounts.Domain;

// A person's account on the public job marketplace. This is deliberately NOT Applications.Candidate:
// that one is the per-tenant applicant record created when someone applies to a job, scoped to a
// single tenant and deduplicated by (TenantId, Email). This one is a tenant-less, GLOBAL identity —
// one account, one email, one password, usable across every tenant's public jobs. It is the login
// subject for the candidate side of the marketplace (register/login/me arrive in a later step); an
// application created later will reference the account it was submitted from.
public sealed class CandidateAccount
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;

    // A CV uploaded once to the account and reused across applications, stored by its object-storage
    // key (same convention as Application.CvFileKey). Null until the candidate uploads one from their
    // profile area — hence nullable from birth even though nothing sets it yet.
    public string? CvFileKey { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private CandidateAccount() { }

    private CandidateAccount(
        Guid id, string email, string passwordHash, string firstName, string lastName, DateTime createdAtUtc)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        CreatedAtUtc = createdAtUtc;
    }

    // Hashing (algorithm, work factor, salt) is an infrastructure concern, so the caller passes an
    // already-computed hash — the domain never sees or stores the plaintext password.
    public static CandidateAccount Register(string email, string passwordHash, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        return new CandidateAccount(
            Guid.NewGuid(), NormalizeEmail(email), passwordHash, firstName.Trim(), lastName.Trim(), DateTime.UtcNow);
    }

    // Stored normalised so the global unique-email constraint is case-insensitive without relying on
    // database collation: "Jane@x.com" and "jane@x.com" are the same account.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
