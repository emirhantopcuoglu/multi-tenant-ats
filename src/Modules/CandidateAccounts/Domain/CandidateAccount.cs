using System.Text.RegularExpressions;

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

    // Versions the account's security state. It is minted into every access token as a claim and
    // compared against this column on each authenticated request; rotating it (password change now,
    // email change later) instantly invalidates every previously issued token. Chosen over a token
    // blacklist (per-request cache lookups, eviction to reason about) and over building refresh-token
    // rotation for candidates (a much larger piece of work than this sprint warrants).
    public Guid SecurityStamp { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;

    // Optional profile data, all nullable: existing accounts predate these fields and the candidate
    // fills them in from the profile page at their own pace. Country/City are constrained to the
    // SupportedCountries catalogue at the application boundary (same split as Jobs: the domain owns
    // self-contained invariants, the boundary owns catalogue membership).
    public string? PhoneNumber { get; private set; }
    public string? Country { get; private set; }
    public string? City { get; private set; }
    public DateOnly? BirthDate { get; private set; }

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
        SecurityStamp = Guid.NewGuid();
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

    // Employment-law floor for most supported countries; anyone younger cannot legally be hired, so a
    // younger birth date is a data-entry error, not an edge case to support.
    public const int MinimumAgeYears = 15;

    // Nobody above this age is realistically job hunting; a birth date beyond it is a typo (1925 for
    // 1995) and rejecting it early beats storing silently wrong data.
    public const int MaximumAgeYears = 100;

    // E.164 caps phone numbers at 15 digits; 7 is a pragmatic floor below which no real number exists.
    // Matched AFTER formatting characters are stripped, so it only sees '+' and digits.
    private const string NormalizedPhonePattern = @"^\+?\d{7,15}$";

    // Email is deliberately not editable here: it is the login identity and the deduplication key for
    // Applications.Candidate records across tenants, so changing it is a bigger operation (verification,
    // re-linking) than a profile edit — it gets its own dedicated flow instead.
    public void UpdateProfile(
        string firstName,
        string lastName,
        string? phoneNumber,
        string? country,
        string? city,
        DateOnly? birthDate)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        if (normalizedPhone is not null && !Regex.IsMatch(normalizedPhone, NormalizedPhonePattern))
            throw new ArgumentException(
                "Phone number must contain 7 to 15 digits, optionally prefixed with '+'.", nameof(phoneNumber));

        var normalizedCountry = NullIfWhiteSpace(country);
        var normalizedCity = NullIfWhiteSpace(city);

        // Residence is stored as a validated (country, city) pair; a half-filled location can never be
        // rendered or filtered on meaningfully, so it is rejected rather than stored.
        if (normalizedCountry is null != normalizedCity is null)
            throw new ArgumentException("Country and city must be provided together.", nameof(city));

        ValidateBirthDate(birthDate);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = normalizedPhone;
        Country = normalizedCountry;
        City = normalizedCity;
        BirthDate = birthDate;
    }

    // Verifying the CURRENT password is the application layer's job (it owns the hasher); by the time
    // this runs the caller has proven ownership and hashed the new secret. The guard runs before any
    // mutation so a rejected change can never rotate the stamp and log the candidate out for nothing.
    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash is required.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        SecurityStamp = Guid.NewGuid();
    }

    // Runs only after the two-phase verification flow proved the caller controls the new mailbox
    // (EmailChangeRequest); nothing else may rename the login identity. Rotating the stamp here is
    // deliberate and stricter than the password case: email IS the login handle, so a takeover via
    // email change must drop every session — including the attacker's — forcing a fresh login that
    // now requires the new address.
    public void ChangeEmail(string newEmail)
    {
        if (string.IsNullOrWhiteSpace(newEmail))
            throw new ArgumentException("Email is required.", nameof(newEmail));

        Email = NormalizeEmail(newEmail);
        SecurityStamp = Guid.NewGuid();
    }

    private static void ValidateBirthDate(DateOnly? birthDate)
    {
        if (birthDate is not { } date)
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (date > today)
            throw new ArgumentException("Birth date cannot be in the future.", nameof(birthDate));
        if (date > today.AddYears(-MinimumAgeYears))
            throw new ArgumentException(
                $"Candidate must be at least {MinimumAgeYears} years old.", nameof(birthDate));
        if (date < today.AddYears(-MaximumAgeYears))
            throw new ArgumentException(
                $"Birth date implies an age above {MaximumAgeYears} years.", nameof(birthDate));
    }

    // People type phone numbers with local formatting ("+90 (532) 123-45-67"); storage keeps one
    // canonical shape so the same number never exists as three different strings. Only known
    // formatting characters are stripped — anything else (letters, symbols) survives into the regex
    // check and fails there, instead of being silently discarded.
    private static readonly char[] PhoneFormattingCharacters = [' ', '-', '(', ')', '.'];

    private static string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        return new string(phoneNumber.Where(c => !PhoneFormattingCharacters.Contains(c)).ToArray());
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
