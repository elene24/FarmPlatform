using FarmPlatform.Domain.Orchards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmPlatform.Infrastructure.Persistence.Configurations;

internal sealed class LegalEntityStatusConfiguration : IEntityTypeConfiguration<LegalEntityStatus>
{
    public void Configure(EntityTypeBuilder<LegalEntityStatus> builder)
    {
        builder.ToTable("LegalEntityStatuses");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.Property(s => s.NameKa).HasMaxLength(150).IsRequired();
        builder.Property(s => s.NameEn).HasMaxLength(150).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();

        // Initial options. Add/rename/deactivate more by inserting rows (or via a future admin screen).
        builder.HasData(
            new LegalEntityStatus(1, "individual_entrepreneur", "ინდივიდუალური მეწარმე", "Individual Entrepreneur", 10),
            new LegalEntityStatus(2, "llc", "შეზღუდული პასუხისმგებლობის საზოგადოება (შპს)", "Limited Liability Company (LLC)", 20),
            new LegalEntityStatus(3, "natural_person", "ფიზიკური პირი", "Individual / Natural Person", 30),
            new LegalEntityStatus(4, "other", "სხვა", "Other", 100));
    }
}
