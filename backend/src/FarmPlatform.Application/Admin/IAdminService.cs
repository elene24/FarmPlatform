using FarmPlatform.Application.Common.Results;

namespace FarmPlatform.Application.Admin;

/// <summary>Read-only views over all accounts for administrators. Callers must already be authorized as admin.</summary>
public interface IAdminService
{
    Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken);

    Task<Result<PagedResult<AdminUserDto>>> ListUsersAsync(AdminUserQuery query, CancellationToken cancellationToken);

    Task<AdminUserDetailDto?> GetUserAsync(Guid adminId, Guid userId, CancellationToken cancellationToken);

    Task<Result<PagedResult<AdminLinkedPickerDto>>> ListLinkedPickersAsync(AdminLinkedQuery query, CancellationToken cancellationToken);
}
