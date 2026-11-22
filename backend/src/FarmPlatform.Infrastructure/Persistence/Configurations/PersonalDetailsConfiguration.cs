using FarmPlatform.Domain.Profiles;
using FarmPlatform.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PersonalDetailsConfiguration : IEntityTypeConfiguration<PersonalDetails>
{
    public void Configure(EntityTypeBuilder<PersonalDetails> builder)
    {
        builder.ToTable("PersonalDetails");
        builder.HasKey(p => p.Id);

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<PersonalDetails>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.UserId).IsUnique();

        builder.Property(p => p.FirstName).HasMaxLength(PersonalDetails.NameMaxLength).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(PersonalDetails.NameMaxLength).IsRequired();

        builder.Property(p => p.PersonalIdNumberCiphertext).HasMaxLength(512).IsRequired();
        builder.Property(p => p.PersonalIdNumberHash).HasMaxLength(64).IsRequired();
        builder.Property(p => p.PersonalIdNumberLast4).HasMaxLength(4).IsRequired();
        // A person (personal ID number) can be registered on only one account.
        builder.HasIndex(p => p.PersonalIdNumberHash).IsUnique();

        builder.Property(p => p.BankAccountCiphertext).HasMaxLength(512).IsRequired();
        builder.Property(p => p.BankAccountLast4).HasMaxLength(4).IsRequired();

        builder.Property(p => p.LegalAddress).HasMaxLength(PersonalDetails.AddressMaxLength).IsRequired();
        builder.Property(p => p.ActualAddress).HasMaxLength(PersonalDetails.AddressMaxLength).IsRequired();

        builder.HasGeoPoint(p => p.Location);
    }
}
