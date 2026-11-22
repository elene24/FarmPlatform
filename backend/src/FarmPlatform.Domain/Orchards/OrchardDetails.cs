using FarmPlatform.Domain.Common;

namespace FarmPlatform.Domain.Orchards;

/// <summary>Input for creating or updating an orchard.</summary>
public sealed record OrchardDetails(
    int LegalEntityStatusId,
    string Name,
    string IdentificationNumber,
    string Address,
    GeoPoint Location,
    decimal AreaHectares);
