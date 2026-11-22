namespace FarmPlatform.Domain.Common;

/// <summary>
/// A WGS84 coordinate as picked on Google Maps. Stored as numeric(9,6) columns:
/// 6 decimal places is ~11 cm precision, more than enough for a house or orchard gate.
/// </summary>
public sealed record GeoPoint
{
    public decimal Latitude { get; private init; }
    public decimal Longitude { get; private init; }

    public GeoPoint(decimal latitude, decimal longitude)
    {
        if (latitude is < -90m or > 90m)
        {
            throw new DomainException("geo.latitude_out_of_range", "Latitude must be between -90 and 90.");
        }

        if (longitude is < -180m or > 180m)
        {
            throw new DomainException("geo.longitude_out_of_range", "Longitude must be between -180 and 180.");
        }

        Latitude = decimal.Round(latitude, 6);
        Longitude = decimal.Round(longitude, 6);
    }
}
