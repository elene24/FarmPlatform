using FarmPlatform.Domain.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PickerProfileConfiguration : IEntityTypeConfiguration<PickerProfile>
{
    public void Configure(EntityTypeBuilder<PickerProfile> builder) => builder.ConfigureRoleProfile("PickerProfiles");
}

internal sealed class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ConfigureRoleProfile("DriverProfiles");

        builder.HasMany(d => d.Vehicles)
            .WithOne()
            .HasForeignKey(v => v.DriverProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Vehicles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OrchardOwnerProfileConfiguration : IEntityTypeConfiguration<OrchardOwnerProfile>
{
    public void Configure(EntityTypeBuilder<OrchardOwnerProfile> builder)
    {
        builder.ConfigureRoleProfile("OrchardOwnerProfiles");

        builder.HasMany(o => o.Orchards)
            .WithOne()
            .HasForeignKey(o => o.OwnerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Orchards).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
