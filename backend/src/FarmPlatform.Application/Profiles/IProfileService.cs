using FarmPlatform.Application.Common.Results;
using FarmPlatform.Domain.Profiles;

namespace FarmPlatform.Application.Profiles;

/// <summary>
/// Registration categories, personal details, vehicles and orchards. Every method takes the signed-in user's id
/// from the session (never from the request), so users can only ever see and change their own data.
/// Records that belong to someone else are reported as "not found".
/// </summary>
public interface IProfileService
{
    Task<ProfilesSummaryDto> GetSummaryAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<ProfilesSummaryDto>> RegisterCategoriesAsync(Guid userId, CategoryRegistrationCommand command, CancellationToken cancellationToken);

    /// <summary>Deactivates a category. The account, personal details, vehicles and orchards are kept.</summary>
    Task<Result> DeactivateCategoryAsync(Guid userId, ProfileCategory category, CancellationToken cancellationToken);

    Task<PersonalDetailsDto?> GetPersonalDetailsAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<PersonalDetailsDto>> SavePersonalDetailsAsync(Guid userId, PersonalDetailsInput input, CancellationToken cancellationToken);

    Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<VehicleDto>> AddVehicleAsync(Guid userId, VehicleInput input, CancellationToken cancellationToken);

    Task<Result<VehicleDto>> UpdateVehicleAsync(Guid userId, Guid vehicleId, VehicleInput input, CancellationToken cancellationToken);

    Task<Result> DeleteVehicleAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrchardDto>> GetOrchardsAsync(Guid userId, CancellationToken cancellationToken);

    Task<OrchardDto?> GetOrchardAsync(Guid userId, Guid orchardId, CancellationToken cancellationToken);

    Task<Result<OrchardDto>> AddOrchardAsync(Guid userId, OrchardInput input, CancellationToken cancellationToken);

    Task<Result<OrchardDto>> UpdateOrchardAsync(Guid userId, Guid orchardId, OrchardInput input, CancellationToken cancellationToken);

    Task<Result> DeleteOrchardAsync(Guid userId, Guid orchardId, CancellationToken cancellationToken);
}
