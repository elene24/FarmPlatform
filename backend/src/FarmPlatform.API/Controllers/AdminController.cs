using FarmPlatform.Api.Errors;
using FarmPlatform.Api.Security;
using FarmPlatform.Application.Admin;
using FarmPlatform.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmPlatform.Api.Controllers;

/// <summary>Read-only admin views over all accounts and linked pickers. Admin role only; everyone else gets 403.</summary>
[ApiController]
[Authorize(Policy = SecurityServiceCollectionExtensions.AdminPolicy)]
[Route("api/v1/admin")]
public sealed class AdminController(IAdminService admin, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> Stats(CancellationToken cancellationToken) =>
        Ok(await admin.GetStatsAsync(cancellationToken));

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> Users(
        [FromQuery] string? role, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var result = await admin.ListUsersAsync(new AdminUserQuery(role, search, page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<AdminUserDetailDto>> UserDetail(Guid id, CancellationToken cancellationToken)
    {
        var user = await admin.GetUserAsync(currentUser.UserId, id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("linked-pickers")]
    public async Task<ActionResult<PagedResult<AdminLinkedPickerDto>>> LinkedPickers(
        [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var result = await admin.ListLinkedPickersAsync(new AdminLinkedQuery(status, search, page, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }
}
