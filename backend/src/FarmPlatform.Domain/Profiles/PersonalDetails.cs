using FarmPlatform.Domain.Common;

namespace FarmPlatform.Domain.Profiles;

/// <summary>
/// Personal information shared by every person-type profile (Picker, Driver, future roles).
/// Exactly one row per user, so someone who is both a Picker and a Driver enters it once.
///
/// Sensitive values (personal ID number, bank account) are never stored in plain text:
///  - *Ciphertext holds the AES-GCM encrypted value (decrypt only when the owner views it);
///  - PersonalIdNumberHash is a keyed HMAC "blind index" so uniqueness can be enforced
///    and duplicates detected without decrypting anything;
///  - *Last4 lets the UI show a masked hint ("•••• 1234") without decrypting.
/// Encryption happens in the application layer (see ISensitiveDataProtector).
/// </summary>
public sealed class PersonalDetails : Entity
{
    public const int NameMaxLength = 100;
    public const int AddressMaxLength = 300;

    private PersonalDetails() { } // EF Core

    public PersonalDetails(
        Guid userId,
        string firstName,
        string lastName,
        ProtectedValue personalIdNumber,
        ProtectedValue bankAccountNumber,
        string legalAddress,
        string actualAddress,
        GeoPoint location)
    {
        UserId = userId;
        Update(firstName, lastName, legalAddress, actualAddress, location);
        SetPersonalIdNumber(personalIdNumber);
        SetBankAccountNumber(bankAccountNumber);
    }

    public Guid UserId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;

    public string PersonalIdNumberCiphertext { get; private set; } = string.Empty;
    public string PersonalIdNumberHash { get; private set; } = string.Empty;
    public string PersonalIdNumberLast4 { get; private set; } = string.Empty;

    public string BankAccountCiphertext { get; private set; } = string.Empty;
    public string BankAccountLast4 { get; private set; } = string.Empty;

    public string LegalAddress { get; private set; } = string.Empty;
    public string ActualAddress { get; private set; } = string.Empty;

    /// <summary>Residential location picked on the map.</summary>
    public GeoPoint Location { get; private set; } = null!;

    public void Update(string firstName, string lastName, string legalAddress, string actualAddress, GeoPoint location)
    {
        FirstName = Require(firstName, NameMaxLength, "personal.first_name_required");
        LastName = Require(lastName, NameMaxLength, "personal.last_name_required");
        LegalAddress = Require(legalAddress, AddressMaxLength, "personal.legal_address_required");
        ActualAddress = Require(actualAddress, AddressMaxLength, "personal.actual_address_required");
        Location = location ?? throw new DomainException("personal.location_required", "Location is required.");
    }

    public void SetPersonalIdNumber(ProtectedValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrEmpty(value.BlindIndex))
        {
            throw new ArgumentException("A blind index is required for the personal ID number.", nameof(value));
        }

        PersonalIdNumberCiphertext = value.Ciphertext;
        PersonalIdNumberHash = value.BlindIndex;
        PersonalIdNumberLast4 = value.Last4;
    }

    public void SetBankAccountNumber(ProtectedValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        BankAccountCiphertext = value.Ciphertext;
        BankAccountLast4 = value.Last4;
    }

    private static string Require(string value, int maxLength, string code)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException(code, "Value is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(code.Replace("_required", "_too_long", StringComparison.Ordinal), $"Value must be at most {maxLength} characters.");
        }

        return trimmed;
    }
}
