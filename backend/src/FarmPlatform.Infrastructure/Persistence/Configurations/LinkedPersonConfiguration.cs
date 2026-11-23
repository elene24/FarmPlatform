using FarmPlatform.Domain.Linked;
using FarmPlatform.Domain.Profiles;
using FarmPlatform.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmPlatform.Infrastructure.Persistence.Configurations;

internal sealed class LinkedPersonConfiguration : IEntityTypeConfiguration<LinkedPerson>
{
    public void Configure(EntityTypeBuilder<LinkedPerson> builder)
    {
        builder.ToTable("LinkedPersons");
        builder.HasKey(l => l.Id);
        builder.Ignore(l => l.CanBeOfferedWork);

        // Deleting the representative's account deletes the people they entered (nobody else may manage them).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.RepresentativeUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => l.RepresentativeUserId);

        builder.Property(l => l.FirstName).HasMaxLength(PersonalDetails.NameMaxLength).IsRequired();
        builder.Property(l => l.LastName).HasMaxLength(PersonalDetails.NameMaxLength).IsRequired();
        builder.Property(l => l.PersonalIdNumberCiphertext).HasMaxLength(512).IsRequired();
        builder.Property(l => l.PersonalIdNumberHash).HasMaxLength(64).IsRequired();
        builder.Property(l => l.PersonalIdNumberLast4).HasMaxLength(4).IsRequired();
        builder.HasIndex(l => l.PersonalIdNumberHash).IsUnique(); // one linked profile per person
        builder.Property(l => l.PhoneNumber).HasMaxLength(PersonalDetails.PhoneMaxLength).IsRequired();
        builder.Property(l => l.LegalAddress).HasMaxLength(PersonalDetails.AddressMaxLength).IsRequired();
        builder.Property(l => l.ActualAddress).HasMaxLength(PersonalDetails.AddressMaxLength).IsRequired();
        builder.HasGeoPoint(l => l.Location);
        builder.Property(l => l.BankAccountCiphertext).HasMaxLength(512);
        builder.Property(l => l.BankAccountLast4).HasMaxLength(4);

        builder.Property(l => l.ConsentStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.ConsentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.ConsentTextVersion).HasMaxLength(20);
        builder.Property(l => l.ConsentTokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(l => l.ConsentTokenHash).IsUnique();
        builder.Property(l => l.ConsentCodeHash).HasMaxLength(64);
    }
}
