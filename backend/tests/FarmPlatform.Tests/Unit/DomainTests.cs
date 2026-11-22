using FarmPlatform.Domain.Common;
using FarmPlatform.Domain.Orchards;
using FarmPlatform.Domain.Profiles;

namespace FarmPlatform.Tests.Unit;

public sealed class DomainTests
{
    private static readonly GeoPoint Tbilisi = new(41.715137m, 44.827096m);

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void GeoPoint_rejects_out_of_range_coordinates(double lat, double lng) =>
        Assert.Throws<DomainException>(() => new GeoPoint((decimal)lat, (decimal)lng));

    [Fact]
    public void GeoPoint_rounds_to_six_decimals() =>
        Assert.Equal(41.715137m, new GeoPoint(41.7151374999m, 44m).Latitude);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public void Vehicle_rejects_invalid_passenger_capacity(int capacity)
    {
        var driver = new DriverProfile(Guid.CreateVersion7());

        var ex = Assert.Throws<DomainException>(() => driver.AddVehicle("AA-123-BB", "Ford Transit", capacity));
        Assert.Equal("vehicle.capacity_out_of_range", ex.Code);
    }

    [Fact]
    public void Driver_can_have_several_vehicles_but_not_the_same_plate_twice()
    {
        var driver = new DriverProfile(Guid.CreateVersion7());

        var first = driver.AddVehicle("aa-123-bb", "Ford Transit", 14);
        driver.AddVehicle("BB-456-CC", "Mercedes Sprinter", 18);

        Assert.Equal("AA123BB", first.LicensePlateNumber);
        Assert.Equal(2, driver.Vehicles.Count);
        Assert.Equal("vehicle.duplicate_plate",
            Assert.Throws<DomainException>(() => driver.AddVehicle("AA 123 BB", "Other", 4)).Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Orchard_rejects_non_positive_area(double area)
    {
        var owner = new OrchardOwnerProfile(Guid.CreateVersion7());

        var ex = Assert.Throws<DomainException>(() =>
            owner.AddOrchard(new OrchardDetails(2, "Apple Garden", "404123456", "Gori", Tbilisi, (decimal)area)));
        Assert.Equal("orchard.area_out_of_range", ex.Code);
    }

    [Fact]
    public void Owner_can_register_several_orchards_each_with_its_own_location()
    {
        var owner = new OrchardOwnerProfile(Guid.CreateVersion7());

        owner.AddOrchard(new OrchardDetails(2, "North", "404123456", "Gori", new GeoPoint(41.98m, 44.11m), 12.5m));
        owner.AddOrchard(new OrchardDetails(2, "South", "404123456", "Kareli", new GeoPoint(42.02m, 43.89m), 3m));

        Assert.Equal(2, owner.Orchards.Count);
        Assert.NotEqual(owner.Orchards.First().Location, owner.Orchards.Last().Location);
    }

    [Fact]
    public void Deactivating_a_category_keeps_it_for_reactivation()
    {
        var picker = new PickerProfile(Guid.CreateVersion7());
        var now = DateTimeOffset.UtcNow;

        picker.Deactivate(now);
        Assert.False(picker.IsActive);
        Assert.Equal(now, picker.DeactivatedAt);

        picker.Reactivate();
        Assert.True(picker.IsActive);
        Assert.Null(picker.DeactivatedAt);
    }
}
