using System.Text.RegularExpressions;

namespace Ats.Modules.CandidateAccounts.Domain;

// Global marketplace account, not tied to any tenant.
// Applications.Candidate is the separate per-tenant applicant record.
public sealed class CandidateAccount
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;

    // Every token carries the stamp, so rotating it ends all sessions.
    public Guid SecurityStamp { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;

    // Country/City are checked against SupportedCountries in the application layer.
    public string? PhoneNumber { get; private set; }
    public string? Country { get; private set; }
    public string? City { get; private set; }
    public DateOnly? BirthDate { get; private set; }

    // The three CV fields only change together, through AttachCv/RemoveCv.
    public string? CvFileKey { get; private set; }

    public string? CvFileName { get; private set; }

    public DateTime? CvUploadedAtUtc { get; private set; }

    public bool HasCv => CvFileKey is not null;

    public string PreferredLanguage { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? EmailVerifiedAtUtc { get; private set; }

    public bool IsEmailVerified => EmailVerifiedAtUtc is not null;

    public CandidateAccountStatus Status { get; private set; }
    public bool IsActive => Status == CandidateAccountStatus.Active;
    public DateTime? FrozenAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    // Consecutive failures. A successful login resets it.
    public int FailedLoginCount { get; private set; }

    // Always expires. A permanent lock would let anyone who knows the email keep the owner out.
    public DateTime? LockoutEndsAtUtc { get; private set; }

    public bool IsLockedOut(DateTime nowUtc) => LockoutEndsAtUtc > nowUtc;

    private CandidateAccount() { }

    private CandidateAccount(
        Guid id, string email, string passwordHash, string firstName, string lastName,
        string preferredLanguage, DateTime createdAtUtc)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        PreferredLanguage = preferredLanguage;
        CreatedAtUtc = createdAtUtc;
        SecurityStamp = Guid.NewGuid();
    }

    public static CandidateAccount Register(
        string email, string passwordHash, string firstName, string lastName, string preferredLanguage)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));
        if (string.IsNullOrWhiteSpace(preferredLanguage))
            throw new ArgumentException("Preferred language is required.", nameof(preferredLanguage));

        return new CandidateAccount(
            Guid.NewGuid(), NormalizeEmail(email), passwordHash, firstName.Trim(), lastName.Trim(),
            preferredLanguage, DateTime.UtcNow);
    }

    public void SetPreferredLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Preferred language is required.", nameof(language));

        PreferredLanguage = language;
    }

    // Lowercased so the unique index is case-insensitive without relying on DB collation.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public const int MinimumAgeYears = 15;

    public const int MaximumAgeYears = 100;

    // E.164 allows at most 15 digits. Matched after formatting characters are stripped.
    private const string NormalizedPhonePattern = @"^\+?\d{7,15}$";

    // Email is not editable here; it changes only through the verified email-change flow.
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

    // Returns the replaced key so the caller can delete that object once the save succeeds.
    public string? AttachCv(string fileKey, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileKey))
            throw new ArgumentException("CV file key is required.", nameof(fileKey));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("CV file name is required.", nameof(fileName));

        var replacedKey = CvFileKey;

        CvFileKey = fileKey;
        CvFileName = fileName;
        CvUploadedAtUtc = DateTime.UtcNow;

        return replacedKey == fileKey ? null : replacedKey;
    }

    public string? RemoveCv()
    {
        var removedKey = CvFileKey;

        CvFileKey = null;
        CvFileName = null;
        CvUploadedAtUtc = null;

        return removedKey;
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash is required.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        SecurityStamp = Guid.NewGuid();

        // A password reset is how a locked-out candidate gets back in.
        ClearLockout();
    }

    public void RegisterFailedLogin(int maxFailedAttempts, TimeSpan lockoutDuration, DateTime nowUtc)
    {
        FailedLoginCount += 1;

        if (FailedLoginCount >= maxFailedAttempts)
        {
            LockoutEndsAtUtc = nowUtc + lockoutDuration;
            // Otherwise every failure after the first lockout would lock the account again.
            FailedLoginCount = 0;
        }
    }

    public void ClearLockout()
    {
        FailedLoginCount = 0;
        LockoutEndsAtUtc = null;
    }

    // Only called after the new address is verified. Rotating the stamp logs out every session,
    // including a hijacked one.
    public void ChangeEmail(string newEmail)
    {
        if (string.IsNullOrWhiteSpace(newEmail))
            throw new ArgumentException("Email is required.", nameof(newEmail));

        Email = NormalizeEmail(newEmail);
        SecurityStamp = Guid.NewGuid();

        // The candidate clicked a link sent to this address, so it is already verified.
        EmailVerifiedAtUtc = DateTime.UtcNow;
    }

    // Returns false if the account was already verified; the first timestamp is kept.
    public bool MarkEmailVerified()
    {
        if (IsEmailVerified)
            return false;

        EmailVerifiedAtUtc = DateTime.UtcNow;
        return true;
    }

    public const string AnonymizedFirstName = "Deleted";
    public const string AnonymizedLastName = "user";

    // .invalid never resolves (RFC 2606). The id keeps it unique under the email index.
    public static string BuildAnonymizedEmail(Guid accountId) =>
        $"deleted-{accountId:N}@account.invalid";

    // The stamp is not rotated: a frozen account stays signed in so it can reactivate itself.
    public void Freeze()
    {
        if (!IsActive)
            throw new InvalidOperationException("Only an active account can be frozen.");

        Status = CandidateAccountStatus.Frozen;
        FrozenAtUtc = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        if (Status != CandidateAccountStatus.Frozen)
            throw new InvalidOperationException("Only a frozen account can be reactivated.");

        Status = CandidateAccountStatus.Active;
        FrozenAtUtc = null;
    }

    // The row stays because applications reference it; personal data is anonymized in place.
    // The placeholder email frees the real address for a new registration.
    public void Delete()
    {
        if (Status == CandidateAccountStatus.Deleted)
            throw new InvalidOperationException("The account is already deleted.");

        Status = CandidateAccountStatus.Deleted;
        DeletedAtUtc = DateTime.UtcNow;
        FrozenAtUtc = null;

        // PasswordHash is kept: overwriting it with a non-hash would make Verify() throw.
        Email = BuildAnonymizedEmail(Id);
        FirstName = AnonymizedFirstName;
        LastName = AnonymizedLastName;
        PhoneNumber = null;
        Country = null;
        City = null;
        BirthDate = null;
        SecurityStamp = Guid.NewGuid();

        _ = RemoveCv();
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

    // Only formatting characters are stripped; anything else is left to fail the pattern check.
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
