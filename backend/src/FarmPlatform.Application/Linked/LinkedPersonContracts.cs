namespace FarmPlatform.Application.Linked;

/// <summary>
/// A picker without an account, entered by their representative. On update, a null PersonalIdNumber or
/// BankAccountNumber keeps the stored value. With PayToRepresentative the person's pay goes to the
/// representative's own bank account (from the representative's personal details) and no account is entered here.
/// </summary>
public sealed record LinkedPersonInput(
    string? FirstName,
    string? LastName,
    string? PersonalIdNumber,
    string? PhoneNumber,
    string? LegalAddress,
    string? ActualAddress,
    decimal? Latitude,
    decimal? Longitude,
    bool PayToRepresentative,
    string? BankAccountNumber);

public sealed record ConsentCodeInput(string? Code);

/// <summary>What the representative sees. The personal ID and bank account are masked.</summary>
public sealed record LinkedPersonDto(
    Guid Id,
    string FirstName,
    string LastName,
    string PersonalIdNumberLast4,
    string PhoneNumber,
    string LegalAddress,
    string ActualAddress,
    decimal Latitude,
    decimal Longitude,
    bool PayToRepresentative,
    string? BankAccountLast4,
    string ConsentStatus,
    string? ConsentMethod,
    DateTimeOffset? ConsentedAt,
    DateTimeOffset? ConsentWithdrawnAt,
    DateTimeOffset? LastConsentSmsAt,
    bool CanResendSms,
    DateTimeOffset CreatedAt);

/// <summary>
/// What the public consent page shows to whoever has the link. Deliberately minimal: no ID numbers, no addresses.
/// </summary>
public sealed record ConsentRequestDto(
    string PersonFirstName,
    string RepresentativeName,
    bool PayToRepresentative,
    string? BankAccountLast4,
    string ConsentStatus);

public enum ConsentResponse
{
    Accept = 1,
    Decline = 2,
    Withdraw = 3,
}
