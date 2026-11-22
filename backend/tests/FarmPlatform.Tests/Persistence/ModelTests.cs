using FarmPlatform.Domain.Profiles;
using FarmPlatform.Domain.Vehicles;
using FarmPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FarmPlatform.Tests.Persistence;

/// <summary>
/// Builds the real Npgsql model and DDL without needing a database, so mapping mistakes
/// (bad keys, missing precision, broken relationships) fail fast in CI.
/// </summary>
public sealed class ModelTests
{
    // The design-time model keeps everything (incl. HasData seed) that the optimized runtime model may drop.
    private static IModel DesignModel(AppDbContext db) => db.GetService<IDesignTimeModel>().Model;

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options);

    [Fact]
    public void Create_script_contains_all_tables()
    {
        using var db = CreateContext();

        var sql = db.Database.GenerateCreateScript();

        foreach (var table in new[]
                 {
                     "AspNetUsers", "PersonalDetails", "PickerProfiles", "DriverProfiles", "Vehicles",
                     "OrchardOwnerProfiles", "Orchards", "LegalEntityStatuses", "DataProtectionKeys",
                 })
        {
            Assert.Contains($"CREATE TABLE \"{table}\"", sql, StringComparison.Ordinal);
        }

        Assert.Contains("numeric(9,6)", sql, StringComparison.Ordinal);
        Assert.Contains("numeric(12,4)", sql, StringComparison.Ordinal);
        Assert.Contains("CK_Vehicles_PassengerCapacity", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Personal_id_blind_index_is_unique_and_each_category_is_one_per_user()
    {
        using var db = CreateContext();

        var personal = DesignModel(db).FindEntityType(typeof(PersonalDetails))!;
        Assert.Contains(personal.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(PersonalDetails.PersonalIdNumberHash));

        foreach (var type in new[] { typeof(PickerProfile), typeof(DriverProfile), typeof(OrchardOwnerProfile) })
        {
            var entity = DesignModel(db).FindEntityType(type)!;
            Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(RoleProfile.UserId));
        }
    }

    [Fact]
    public void License_plate_is_unique_per_driver_not_globally()
    {
        using var db = CreateContext();
        var vehicle = DesignModel(db).FindEntityType(typeof(Vehicle))!;

        Assert.Contains(vehicle.GetIndexes(), i => i.IsUnique && i.Properties.Count == 2);
        Assert.Contains(vehicle.GetIndexes(), i => !i.IsUnique && i.Properties.Single().Name == nameof(Vehicle.LicensePlateNumber));
    }

    [Fact]
    public void Legal_entity_statuses_are_seeded()
    {
        using var db = CreateContext();

        var seed = DesignModel(db).FindEntityType(typeof(Domain.Orchards.LegalEntityStatus))!.GetSeedData().ToList();

        Assert.Equal(4, seed.Count);
    }
}
