using FarmPlatform.Domain.Orchards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmPlatform.Infrastructure.Persistence.Configurations;

internal sealed class OrchardConfiguration : IEntityTypeConfiguration<Orchard>
{
    public void Configure(EntityTypeBuilder<Orchard> builder)
    {
        builder.ToTable("Orchards", t => t.HasCheckConstraint("CK_Orchards_AreaHectares", "\"AreaHectares\" > 0"));
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).HasMaxLength(Orchard.NameMaxLength).IsRequired();
        builder.Property(o => o.IdentificationNumber).HasMaxLength(Orchard.IdentificationNumberMaxLength).IsRequired();
        builder.Property(o => o.Address).HasMaxLength(Orchard.AddressMaxLength).IsRequired();
        builder.Property(o => o.AreaHectares).HasPrecision(12, 4);
        builder.HasGeoPoint(o => o.Location);

        builder.HasOne(o => o.LegalEntityStatus)
            .WithMany()
            .HasForeignKey(o => o.LegalEntityStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // Not unique on purpose (one company, many orchards) - indexed for lookups and future verification.
        builder.HasIndex(o => o.IdentificationNumber);
    }
}
