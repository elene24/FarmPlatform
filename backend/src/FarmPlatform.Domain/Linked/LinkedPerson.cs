using FarmPlatform.Domain.Common;
using FarmPlatform.Domain.Profiles;

namespace FarmPlatform.Domain.Linked;

/// <summary>
/// A picker who has no account and is represented by a registered user (family member, neighbour, brigade
/// leader...). The representative may answer availability for picking events on the person's behalf and, if
/// agreed, receive the person's pay. Nothing is allowed until the person consents by SMS (code or link).
///
/// Consent covers a specific phone number and payment arrangement: changing either asks for consent again.
/// </summary>
public sealed class LinkedPerson : Entity
{
    public const int MaxPerRepresentative = 30;
    public const int MaxCodeAttempts = 5;

    private LinkedPerson() { } // EF Core

    public LinkedPerson(
        Guid representativeUserId,
        string firstName,
        string lastName,
        ProtectedValue personalIdNumber,
        string legalAddress,
        string actualAddress,
        GeoPoint location,
        string consentTokenHash)
    {
        RepresentativeUserId = representativeUserId;
        UpdateDetails(firstName, lastName, legalAddress, actualAddress, location);
        SetPersonalIdNumber(personalIdNumber);
        ConsentTokenHash = consentTokenHash;
        ConsentStatus = ConsentStatus.Pending;
    }

    public Guid RepresentativeUserId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;

    public string PersonalIdNumberCiphertext { get; private set; } = string.Empty;
    public string PersonalIdNumberHash { get; private set; } = string.Empty;
    public string PersonalIdNumberLast4 { get; private set; } = string.Empty;

    /// <summary>The person's own mobile number (E.164). Consent SMS messages go here.</summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    public string LegalAddress { get; private set; } = string.Empty;
    public string ActualAddress { get; private set; } = string.Empty;
    public GeoPoint Location { get; private set; } = null!;

    /// <summary>True: pay goes to the representative's bank account (from their personal details).</summary>
    public bool PayToRepresentative { get; private set; }

    /// <summary>The person's own account; null when <see cref="PayToRepresentative"/> is true.</summary>
    public string? BankAccountCiphertext { get; private set; }
    public string? BankAccountLast4 { get; private set; }

    // ---- Consent ----
    public ConsentStatus ConsentStatus { get; private set; }
    public ConsentMethod? ConsentMethod { get; private set; }
    public DateTimeOffset? ConsentedAt { get; private set; }
    public DateTimeOffset? ConsentWithdrawnAt { get; private set; }

    /// <summary>Version of the consent text the person agreed to (see ConsentTexts in the application layer).</summary>
    public string? ConsentTextVersion { get; private set; }

    /// <summary>SHA-256 of the secret in the latest consent link (each consent SMS carries a new link).</summary>
    public string ConsentTokenHash { get; private set; } = string.Empty;

    /// <summary>SHA-256 of the current 6-digit code (never stored in plain text).</summary>
    public string? ConsentCodeHash { get; private set; }
    public DateTimeOffset? ConsentCodeExpiresAt { get; private set; }
    public int ConsentCodeAttempts { get; private set; }

    public DateTimeOffset? LastConsentSmsAt { get; private set; }
    public DateTimeOffset? ConsentSmsWindowStart { get; private set; }
    public int ConsentSmsInWindow { get; private set; }

    public bool CanBeOfferedWork => ConsentStatus == ConsentStatus.Confirmed;

    public void UpdateDetails(string firstName, string lastName, string legalAddress, string actualAddress, GeoPoint location)
    {
        FirstName = Require(firstName, PersonalDetails.NameMaxLength, "linked.first_name_required");
        LastName = Require(lastName, PersonalDetails.NameMaxLength, "linked.last_name_required");
        LegalAddress = Require(legalAddress, PersonalDetails.AddressMaxLength, "linked.legal_address_required");
        ActualAddress = Require(actualAddress, PersonalDetails.AddressMaxLength, "linked.actual_address_required");
        Location = location ?? throw new DomainException("linked.location_required", "Location is required.");
    }

    public void SetPersonalIdNumber(ProtectedValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        PersonalIdNumberCiphertext = value.Ciphertext;
        PersonalIdNumberHash = value.BlindIndex;
        PersonalIdNumberLast4 = value.Last4;
    }

    /// <summary>
    /// Sets the phone number and payment arrangement. Returns true when this changes what consent covers, in which
    /// case confirmed consent is reset and a new consent SMS must be sent.
    /// </summary>
    /// <param name="ownBankAccount">The person's own account; null keeps the stored one (ignored when paying the representative).</param>
    public bool SetContactAndPayout(string phoneNumber, bool payToRepresentative, ProtectedValue? ownBankAccount)
    {
        var phone = phoneNumber?.Trim() ?? string.Empty;
        if (phone.Length is 0 or > PersonalDetails.PhoneMaxLength || !phone.StartsWith('+'))
        {
            throw new DomainException("linked.phone_invalid", "A mobile number in international format is required.");
        }

        if (!payToRepresentative && ownBankAccount is null && BankAccountCiphertext is null)
        {
            throw new DomainException("linked.bank_account_required", "The person's bank account is required.");
        }

        var changed = phone != PhoneNumber
            || payToRepresentative != PayToRepresentative
            || (!payToRepresentative && ownBankAccount is not null && ownBankAccount.Ciphertext != BankAccountCiphertext);

        PhoneNumber = phone;
        PayToRepresentative = payToRepresentative;
        if (payToRepresentative)
        {
            BankAccountCiphertext = null;
            BankAccountLast4 = null;
        }
        else if (ownBankAccount is not null)
        {
            BankAccountCiphertext = ownBankAccount.Ciphertext;
            BankAccountLast4 = ownBankAccount.Last4;
        }

        if (changed && ConsentStatus == ConsentStatus.Confirmed)
        {
            ConsentStatus = ConsentStatus.Pending;
            ConsentMethod = null;
            ConsentedAt = null;
        }

        return changed;
    }

    /// <summary>
    /// Whether another consent SMS may be sent now: at most 5 per 24 hours, and (for plain resends) 1 minute apart.
    /// A changed request (new phone or payment arrangement) skips the 1-minute wait but not the daily cap.
    /// </summary>
    public bool CanSendConsentSms(DateTimeOffset now, bool requestChanged = false) =>
        (requestChanged || LastConsentSmsAt is null || now - LastConsentSmsAt >= TimeSpan.FromMinutes(1))
        && (ConsentSmsWindowStart is null || now - ConsentSmsWindowStart >= TimeSpan.FromHours(24) || ConsentSmsInWindow < 5);

    /// <summary>Stores a fresh code (as a hash) for a new consent SMS and puts the request back to pending.</summary>
    public void IssueConsentCode(string codeHash, DateTimeOffset now, bool requestChanged = false)
    {
        if (ConsentStatus == ConsentStatus.Confirmed)
        {
            throw new DomainException("linked.already_confirmed", "Consent has already been given.");
        }

        if (!CanSendConsentSms(now, requestChanged))
        {
            throw new DomainException("linked.sms_too_soon", "Please wait before sending another SMS.");
        }

        ConsentStatus = ConsentStatus.Pending;
        ConsentCodeHash = codeHash;
        ConsentCodeExpiresAt = now.AddDays(7);
        ConsentCodeAttempts = 0;
        LastConsentSmsAt = now;
        if (ConsentSmsWindowStart is null || now - ConsentSmsWindowStart >= TimeSpan.FromHours(24))
        {
            ConsentSmsWindowStart = now;
            ConsentSmsInWindow = 0;
        }

        ConsentSmsInWindow++;
    }

    /// <summary>A new consent link replaces the previous one (each consent SMS carries a fresh link).</summary>
    public void RotateConsentToken(string tokenHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ConsentTokenHash = tokenHash;
    }

    /// <summary>Checks a code entered by the representative. Wrong codes count towards a limit per SMS.</summary>
    public bool TryConfirmWithCode(string codeHash, string consentTextVersion, DateTimeOffset now)
    {
        if (ConsentStatus != ConsentStatus.Pending || ConsentCodeHash is null)
        {
            throw new DomainException("linked.no_pending_request", "There is no pending consent request.");
        }

        if (ConsentCodeExpiresAt < now || ConsentCodeAttempts >= MaxCodeAttempts)
        {
            throw new DomainException("linked.code_expired", "The code has expired. Send a new SMS.");
        }

        if (!string.Equals(codeHash, ConsentCodeHash, StringComparison.Ordinal))
        {
            ConsentCodeAttempts++;
            return false;
        }

        Confirm(Linked.ConsentMethod.Code, consentTextVersion, now);
        return true;
    }

    /// <summary>The person agreed via the link in the SMS (allowed again after declining or withdrawing).</summary>
    public void ConfirmViaLink(string consentTextVersion, DateTimeOffset now)
    {
        if (ConsentStatus == ConsentStatus.Confirmed)
        {
            return;
        }

        Confirm(Linked.ConsentMethod.Link, consentTextVersion, now);
    }

    public void Decline(DateTimeOffset now)
    {
        if (ConsentStatus == ConsentStatus.Confirmed)
        {
            Withdraw(now);
            return;
        }

        ConsentStatus = ConsentStatus.Declined;
        ConsentCodeHash = null;
        ConsentWithdrawnAt = now;
    }

    /// <summary>The person withdraws consent they gave earlier.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        if (ConsentStatus != ConsentStatus.Confirmed)
        {
            return;
        }

        ConsentStatus = ConsentStatus.Revoked;
        ConsentWithdrawnAt = now;
        ConsentCodeHash = null;
    }

    private void Confirm(ConsentMethod method, string consentTextVersion, DateTimeOffset now)
    {
        ConsentStatus = ConsentStatus.Confirmed;
        ConsentMethod = method;
        ConsentedAt = now;
        ConsentTextVersion = consentTextVersion;
        ConsentWithdrawnAt = null;
        ConsentCodeHash = null;
        ConsentCodeExpiresAt = null;
        ConsentCodeAttempts = 0;
    }

    private static string Require(string value, int maxLength, string code)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw new DomainException(code, "Value is required.");
        }

        return trimmed;
    }
}
