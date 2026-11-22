namespace FarmPlatform.Application.Profiles;

// ---------- Inputs (request bodies). Nullable so missing fields are reported with translatable codes. ----------

/// <summary>
/// Personal details shared by the Picker and Driver categories. On update, a null PersonalIdNumber,
/// BankAccountNumber or AccountHolderPersonalIdNumber means "keep the stored value" - the API never sends these
/// back in full. When BankAccountOwnedByOther is true, the account holder's name and personal ID are required.
/// </summary>
public sealed record PersonalDetailsInput(
    string? FirstName,
    string? LastName,
    string? PersonalIdNumber,
    string? BankAccountNumber,
    string? LegalAddress,
    string? ActualAddress,
    decimal? Latitude,
    decimal? Longitude,
    string? AlternativePhoneNumber = null,
    bool BankAccountOwnedByOther = false,
    string? AccountHolderFirstName = null,
    string? AccountHolderLastName = null,
    string? AccountHolderPersonalIdNumber = null);

public sealed record VehicleInput(string? LicensePlateNumber, string? Model, int? PassengerCapacity);

public sealed record OrchardInput(
    int? LegalEntityStatusId,
    string? Name,
    string? IdentificationNumber,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AreaHectares);

/// <summary>
/// Steps 3-5 of the registration wizard, saved atomically. Also used to add categories later: personal details
/// are only needed if the user has none yet; vehicles/orchards only if the category has none yet.
/// </summary>
public sealed record CategoryRegistrationCommand(
    bool Picker,
    bool Driver,
    bool OrchardOwner,
    PersonalDetailsInput? PersonalDetails,
    IReadOnlyList<VehicleInput>? Vehicles,
    IReadOnlyList<OrchardInput>? Orchards);

// ---------- Outputs. Sensitive values are masked: only the last 4 characters are ever returned. ----------

public sealed record PersonalDetailsDto(
    string FirstName,
    string LastName,
    string PersonalIdNumberLast4,
    string BankAccountLast4,
    string LegalAddress,
    string ActualAddress,
    decimal Latitude,
    decimal Longitude,
    string? AlternativePhoneNumber,
    bool BankAccountOwnedByOther,
    string? AccountHolderFirstName,
    string? AccountHolderLastName,
    string? AccountHolderPersonalIdLast4,
    DateTimeOffset UpdatedAt);

public sealed record VehicleDto(Guid Id, string LicensePlateNumber, string Model, int PassengerCapacity);

public sealed record OrchardDto(
    Guid Id,
    int LegalEntityStatusId,
    string LegalEntityStatusCode,
    string Name,
    string IdentificationNumber,
    string Address,
    decimal Latitude,
    decimal Longitude,
    decimal AreaHectares);

/// <summary>State of one registration category for the current user.</summary>
public sealed record CategoryStatusDto(bool Registered, bool Active, DateTimeOffset? RegisteredAt);

public sealed record PickerSummaryDto(CategoryStatusDto Status);

public sealed record DriverSummaryDto(CategoryStatusDto Status, IReadOnlyList<VehicleDto> Vehicles);

public sealed record OrchardOwnerSummaryDto(CategoryStatusDto Status, IReadOnlyList<OrchardDto> Orchards);

/// <summary>Everything the dashboard needs in one call (GET /api/v1/profiles).</summary>
public sealed record ProfilesSummaryDto(
    PersonalDetailsDto? PersonalDetails,
    PickerSummaryDto Picker,
    DriverSummaryDto Driver,
    OrchardOwnerSummaryDto OrchardOwner);
