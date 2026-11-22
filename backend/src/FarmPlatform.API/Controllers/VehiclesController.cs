using FarmPlatform.Api.Errors;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Application.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmPlatform.Api.Controllers;

/// <summary>The signed-in driver's vehicles. Ids of other users' vehicles answer 404.</summary>
[ApiController]
[Authorize]
[Route("api/v1/vehicles")]
public sealed class VehiclesController(IProfileService profiles, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await profiles.GetVehiclesAsync(currentUser.UserId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<VehicleDto>> Add(VehicleInput input, CancellationToken cancellationToken)
    {
        var result = await profiles.AddVehicleAsync(currentUser.UserId, input, cancellationToken);
        return result.IsSuccess ? Created($"/api/v1/vehicles/{result.Value!.Id}", result.Value) : this.ToProblem(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<VehicleDto>> Update(Guid id, VehicleInput input, CancellationToken cancellationToken)
    {
        var result = await profiles.UpdateVehicleAsync(currentUser.UserId, id, input, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await profiles.DeleteVehicleAsync(currentUser.UserId, id, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }
}
