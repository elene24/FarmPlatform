using FarmPlatform.Domain.Common;

namespace FarmPlatform.Domain.Orchards;

/// <summary>An orchard owned (or represented) by an orchard-owner profile. Each has its own location.</summary>
public sealed class Orchard : Entity
{
    public const int NameMaxLength = 200;
    public const int IdentificationNumberMaxLength = 20;
    public const int AddressMaxLength = 300;
    public const decimal MaxAreaHectares = 100_000m;

    private Orchard() { } // EF Core

    internal Orchard(Guid ownerProfileId, OrchardDetails details)
    {
        OwnerProfileId = ownerProfileId;
        Update(details);
    }

    public Guid OwnerProfileId { get; private set; }

    public int LegalEntityStatusId { get; private set; }
    public LegalEntityStatus? LegalEntityStatus { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Business identification number (9 digits for Georgian companies; 11-digit personal number
    /// for individual entrepreneurs and natural persons). Deliberately NOT unique: one company can
    /// operate several orchards, and the same entity can be represented by more than one account.
    /// </summary>
    public string IdentificationNumber { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public GeoPoint Location { get; private set; } = null!;

    /// <summary>Area in hectares, numeric(12,4).</summary>
    public decimal AreaHectares { get; private set; }

    public void Update(OrchardDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var name = details.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > NameMaxLength)
        {
            throw new DomainException("orchard.name_invalid", "Orchard name is required.");
        }

        var idNumber = details.IdentificationNumber?.Trim() ?? string.Empty;
        if (idNumber.Length is 0 or > IdentificationNumberMaxLength)
        {
            throw new DomainException("orchard.id_number_invalid", "Identification number is required.");
        }

        var address = details.Address?.Trim() ?? string.Empty;
        if (address.Length is 0 or > AddressMaxLength)
        {
            throw new DomainException("orchard.address_invalid", "Orchard address is required.");
        }

        if (details.AreaHectares is <= 0m or > MaxAreaHectares)
        {
            throw new DomainException("orchard.area_out_of_range", "Orchard area must be greater than zero.");
        }

        LegalEntityStatusId = details.LegalEntityStatusId;
        Name = name;
        IdentificationNumber = idNumber;
        Address = address;
        Location = details.Location ?? throw new DomainException("orchard.location_required", "Location is required.");
        AreaHectares = decimal.Round(details.AreaHectares, 4);
    }
}
