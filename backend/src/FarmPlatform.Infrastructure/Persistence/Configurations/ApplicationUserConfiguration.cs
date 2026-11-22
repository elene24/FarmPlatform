using FarmPlatform.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.ProfilePhotoKey).HasMaxLength(ApplicationUser.ProfilePhotoKeyMaxLength);
        // Defaults ("ka", true) come from the CLR initializers. No database defaults: EF would skip
        // inserting IsActive=false because false is the CLR default for bool.
        builder.Property(u => u.PreferredLanguage).HasMaxLength(5).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.TermsVersion).HasMaxLength(20);

        // Phone numbers are looked up (duplicates, future SMS login) but not unique:
        // family members sometimes share one phone.
        builder.HasIndex(u => u.PhoneNumber);
    }
}
