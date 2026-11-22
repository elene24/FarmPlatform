using FarmPlatform.Domain.Common;
using FarmPlatform.Domain.Profiles;
using FarmPlatform.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmPlatform.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>Maps a <see cref="GeoPoint"/> to Latitude/Longitude numeric(9,6) columns on the owning table.</summary>
    public static void HasGeoPoint<T>(this EntityTypeBuilder<T> builder, System.Linq.Expressions.Expression<Func<T, GeoPoint?>> property)
        where T : class
    {
        builder.ComplexProperty(property, location =>
        {
            location.Property(p => p.Latitude).HasColumnName("Latitude").HasPrecision(9, 6);
            location.Property(p => p.Longitude).HasColumnName("Longitude").HasPrecision(9, 6);
        });
    }

    /// <summary>Common mapping for the per-category role tables: one row per user, cascade with the account.</summary>
    public static void ConfigureRoleProfile<T>(this EntityTypeBuilder<T> builder, string tableName)
        where T : RoleProfile
    {
        builder.ToTable(tableName);
        builder.HasKey(p => p.Id);
        builder.Ignore(p => p.Category);

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<T>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // "One user can have one PickerProfile/DriverProfile initially" -> unique per user.
        // If multiple profiles per category are ever needed, drop this unique index.
        builder.HasIndex(p => p.UserId).IsUnique();
        builder.HasIndex(p => p.IsActive);
    }
}
