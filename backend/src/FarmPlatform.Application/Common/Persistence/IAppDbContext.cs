using FarmPlatform.Domain.Linked;
using FarmPlatform.Domain.Orchards;
using FarmPlatform.Domain.Profiles;
using FarmPlatform.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace FarmPlatform.Application.Common.Persistence;

/// <summary>
/// The application's view of the database. Use cases query and save through this directly;
/// we intentionally do not wrap EF Core in generic repositories (DbContext already is a
/// unit of work + repository). Identity users are reached through ASP.NET Core Identity instead.
/// </summary>
public interface IAppDbContext
{
    DbSet<PersonalDetails> PersonalDetails { get; }
    DbSet<PickerProfile> PickerProfiles { get; }
    DbSet<DriverProfile> DriverProfiles { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<OrchardOwnerProfile> OrchardOwnerProfiles { get; }
    DbSet<Orchard> Orchards { get; }
    DbSet<LegalEntityStatus> LegalEntityStatuses { get; }
    DbSet<LinkedPerson> LinkedPersons { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
