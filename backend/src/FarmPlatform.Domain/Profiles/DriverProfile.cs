using FarmPlatform.Domain.Common;
using FarmPlatform.Domain.Vehicles;

namespace FarmPlatform.Domain.Profiles;

/// <summary>The Driver category. Owns zero or more vehicles.</summary>
public sealed class DriverProfile : RoleProfile
{
    public const int MaxVehicles = 10;

    private readonly List<Vehicle> _vehicles = [];

    private DriverProfile() { } // EF Core

    public DriverProfile(Guid userId) : base(userId) { }

    public override ProfileCategory Category => ProfileCategory.Driver;

    public IReadOnlyCollection<Vehicle> Vehicles => _vehicles.AsReadOnly();

    public Vehicle AddVehicle(string licensePlateNumber, string model, int passengerCapacity)
    {
        if (_vehicles.Count >= MaxVehicles)
        {
            throw new DomainException("vehicle.limit_reached", $"A driver can register at most {MaxVehicles} vehicles.");
        }

        var normalizedPlate = Vehicle.NormalizePlate(licensePlateNumber);
        if (_vehicles.Any(v => v.LicensePlateNumber == normalizedPlate))
        {
            throw new DomainException("vehicle.duplicate_plate", "This vehicle is already registered on your profile.");
        }

        var vehicle = new Vehicle(Id, licensePlateNumber, model, passengerCapacity);
        _vehicles.Add(vehicle);
        return vehicle;
    }

    public void UpdateVehicle(Guid vehicleId, string licensePlateNumber, string model, int passengerCapacity)
    {
        var vehicle = _vehicles.FirstOrDefault(v => v.Id == vehicleId)
            ?? throw new DomainException("vehicle.not_found", "Vehicle not found.");

        var normalizedPlate = Vehicle.NormalizePlate(licensePlateNumber);
        if (_vehicles.Any(v => v.Id != vehicleId && v.LicensePlateNumber == normalizedPlate))
        {
            throw new DomainException("vehicle.duplicate_plate", "This vehicle is already registered on your profile.");
        }

        vehicle.Update(licensePlateNumber, model, passengerCapacity);
    }

    public void RemoveVehicle(Guid vehicleId)
    {
        var vehicle = _vehicles.FirstOrDefault(v => v.Id == vehicleId)
            ?? throw new DomainException("vehicle.not_found", "Vehicle not found.");
        _vehicles.Remove(vehicle);
    }
}
