using FarmPlatform.Application.Linked;
using FarmPlatform.Application.Profiles;

namespace FarmPlatform.Application.Admin;

public sealed record AdminStatsDto(
    int Users,
    int Pickers,
    int Drivers,
    int OrchardOwners,
    int Admins,
    int LinkedPickers,
    int LinkedPickersConfirmed);

/// <summary>Filters for the account list. Role: all, picker, driver, orchard_owner, admin, none (no category yet).</summary>
public sealed record AdminUserQuery(string? Role, string? Search, int? Page, int? PageSize);

/// <summary>Filters for the linked-picker list. Status: all, pending, confirmed, declined, revoked.</summary>
public sealed record AdminLinkedQuery(string? Status, string? Search, int? Page, int? PageSize);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>One account in the admin list. No sensitive numbers: names, contact details and categories only.</summary>
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string? PhoneNumber,
    bool EmailConfirmed,
    bool IsActive,
    bool IsAdmin,
    string? FirstName,
    string? LastName,
    bool Picker,
    bool Driver,
    bool OrchardOwner,
    int LinkedPickers,
    DateTimeOffset CreatedAt);

/// <summary>Everything about one account, with ID numbers and IBANs masked to their last 4 digits.</summary>
public sealed record AdminUserDetailDto(
    AdminUserDto Account,
    string PreferredLanguage,
    DateTimeOffset? TermsAcceptedAt,
    ProfilesSummaryDto Profiles,
    IReadOnlyList<LinkedPersonDto> LinkedPickers);

public sealed record AdminLinkedPickerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string PersonalIdNumberLast4,
    string PhoneNumber,
    string ActualAddress,
    bool PayToRepresentative,
    string ConsentStatus,
    DateTimeOffset? ConsentedAt,
    DateTimeOffset CreatedAt,
    Guid RepresentativeId,
    string RepresentativeEmail,
    string? RepresentativeFirstName,
    string? RepresentativeLastName);
