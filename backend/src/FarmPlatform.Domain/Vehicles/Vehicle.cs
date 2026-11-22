using FarmPlatform.Domain.Common;

namespace FarmPlatform.Domain.Vehicles;

/// <summary>A vehicle registered by a driver. Stored separately from personal information.</summary>
public sealed class Vehicle : Entity
{
    public const int PlateMaxLength = 15;
    public const int ModelMaxLength = 100;
    public const int MinPassengerCapacity = 1;
    public const int MaxPassengerCapacity = 60; // a full-size bus

    private Vehicle() { } // EF Core

    internal Vehicle(Guid driverProfileId, string licensePlateNumber, string model, int passengerCapacity)
    {
        DriverProfileId = driverProfileId;
        Update(licensePlateNumber, model, passengerCapacity);
    }

    public Guid DriverProfileId { get; private set; }

    /// <summary>Normalized: upper-case, no spaces or dashes (e.g. "AA123BB").</summary>
    public string LicensePlateNumber { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    /// <summary>Number of seats available for passengers (excluding the driver).</summary>
    public int PassengerCapacity { get; private set; }

    public void Update(string licensePlateNumber, string model, int passengerCapacity)
    {
        var plate = NormalizePlate(licensePlateNumber);
        if (plate.Length is 0 or > PlateMaxLength)
        {
            throw new DomainException("vehicle.plate_invalid", "License plate number is invalid.");
        }

        var trimmedModel = model?.Trim() ?? string.Empty;
        if (trimmedModel.Length is 0 or > ModelMaxLength)
        {
            throw new DomainException("vehicle.model_invalid", "Vehicle model is required.");
        }

        if (passengerCapacity is < MinPassengerCapacity or > MaxPassengerCapacity)
        {
            throw new DomainException(
                "vehicle.capacity_out_of_range",
                $"Passenger capacity must be between {MinPassengerCapacity} and {MaxPassengerCapacity}.");
        }

        LicensePlateNumber = plate;
        Model = trimmedModel;
        PassengerCapacity = passengerCapacity;
    }

    public static string NormalizePlate(string? plate) =>
        new((plate ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
