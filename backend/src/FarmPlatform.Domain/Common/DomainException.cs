namespace FarmPlatform.Domain.Common;

/// <summary>Thrown when a business rule is violated. Mapped to HTTP 400/409 by the API.</summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    /// <summary>Stable, machine-readable error code the frontend can translate (e.g. "geo.latitude_out_of_range").</summary>
    public string Code { get; } = code;
}
