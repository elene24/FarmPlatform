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
    public const int PhoneMaxLength = 20;

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

    /// <summary>
    /// True when the bank account belongs to someone else (e.g. a family member). The holder's name and personal
    /// ID number are then required; the ID number is encrypted like the user's own.
    /// </summary>
    public bool BankAccountOwnedByOther { get; private set; }

    public string? AccountHolderFirstName { get; private set; }
    public string? AccountHolderLastName { get; private set; }
    public string? AccountHolderPersonalIdCiphertext { get; private set; }
    public string? AccountHolderPersonalIdLast4 { get; private set; }

    /// <summary>Optional secondary contact number, E.164 (e.g. +995599123456).</summary>
    public string? AlternativePhoneNumber { get; private set; }

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

    /// <summary>The account belongs to the user: clears any stored holder details.</summary>
    public void SetBankAccountOwnedBySelf()
    {
        BankAccountOwnedByOther = false;
        AccountHolderFirstName = null;
        AccountHolderLastName = null;
        AccountHolderPersonalIdCiphertext = null;
        AccountHolderPersonalIdLast4 = null;
    }

    /// <summary>The account belongs to someone else. Pass a null <paramref name="personalId"/> to keep the stored one.</summary>
    public void SetBankAccountHolder(string firstName, string lastName, ProtectedValue? personalId)
    {
        if (personalId is null && AccountHolderPersonalIdCiphertext is null)
        {
            throw new DomainException("personal.account_holder_id_required", "The account holder's personal ID number is required.");
        }

        AccountHolderFirstName = Require(firstName, NameMaxLength, "personal.account_holder_first_name_required");
        AccountHolderLastName = Require(lastName, NameMaxLength, "personal.account_holder_last_name_required");
        if (personalId is not null)
        {
            AccountHolderPersonalIdCiphertext = personalId.Ciphertext;
            AccountHolderPersonalIdLast4 = personalId.Last4;
        }

        BankAccountOwnedByOther = true;
    }

    /// <summary>Null or empty removes the alternative number. Expects an already normalized E.164 value.</summary>
    public void SetAlternativePhoneNumber(string? phoneNumber)
    {
        var trimmed = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        if (trimmed is not null && (trimmed.Length > PhoneMaxLength || !trimmed.StartsWith('+')))
        {
            throw new DomainException("personal.alternative_phone_invalid", "Alternative phone number must be in international format.");
        }

        AlternativePhoneNumber = trimmed;
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
