using FarmPlatform.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmPlatform.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", t => t.HasCheckConstraint(
            "CK_Vehicles_PassengerCapacity",
            $"\"PassengerCapacity\" BETWEEN {Vehicle.MinPassengerCapacity} AND {Vehicle.MaxPassengerCapacity}"));
        builder.HasKey(v => v.Id);

        builder.Property(v => v.LicensePlateNumber).HasMaxLength(Vehicle.PlateMaxLength).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(Vehicle.ModelMaxLength).IsRequired();

        // Same plate can't be added twice to one driver...
        builder.HasIndex(v => new { v.DriverProfileId, v.LicensePlateNumber }).IsUnique();
        // ...but is not globally unique: a family car may legitimately be driven by two registered drivers.
        builder.HasIndex(v => v.LicensePlateNumber);
    }
}
