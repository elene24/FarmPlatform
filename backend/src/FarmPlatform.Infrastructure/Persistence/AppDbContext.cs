using FarmPlatform.Application.Common.Persistence;
using FarmPlatform.Domain.Linked;
using FarmPlatform.Domain.Orchards;
using FarmPlatform.Domain.Profiles;
using FarmPlatform.Domain.Vehicles;
using FarmPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FarmPlatform.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options),
      IAppDbContext,
      IDataProtectionKeyContext
{
    public DbSet<PersonalDetails> PersonalDetails => Set<PersonalDetails>();
    public DbSet<PickerProfile> PickerProfiles => Set<PickerProfile>();
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<OrchardOwnerProfile> OrchardOwnerProfiles => Set<OrchardOwnerProfile>();
    public DbSet<Orchard> Orchards => Set<Orchard>();
    public DbSet<LegalEntityStatus> LegalEntityStatuses => Set<LegalEntityStatus>();
    public DbSet<LinkedPerson> LinkedPersons => Set<LinkedPerson>();

    /// <summary>
    /// ASP.NET Core Data Protection keys (they encrypt auth cookies and email/password-reset tokens).
    /// Stored in the database so they survive Render redeploys; otherwise every deploy would log
    /// everybody out and invalidate pending reset links.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity tables first, then our configurations (which may refine ApplicationUser).
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
