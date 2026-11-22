using FarmPlatform.Api.Errors;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Application.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmPlatform.Api.Controllers;

/// <summary>The signed-in owner's orchards. Ids of other users' orchards answer 404.</summary>
[ApiController]
[Authorize]
[Route("api/v1/orchards")]
public sealed class OrchardsController(IProfileService profiles, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrchardDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await profiles.GetOrchardsAsync(currentUser.UserId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrchardDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var orchard = await profiles.GetOrchardAsync(currentUser.UserId, id, cancellationToken);
        return orchard is null ? NotFound() : Ok(orchard);
    }

    [HttpPost]
    public async Task<ActionResult<OrchardDto>> Add(OrchardInput input, CancellationToken cancellationToken)
    {
        var result = await profiles.AddOrchardAsync(currentUser.UserId, input, cancellationToken);
        return result.IsSuccess ? Created($"/api/v1/orchards/{result.Value!.Id}", result.Value) : this.ToProblem(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrchardDto>> Update(Guid id, OrchardInput input, CancellationToken cancellationToken)
    {
        var result = await profiles.UpdateOrchardAsync(currentUser.UserId, id, input, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await profiles.DeleteOrchardAsync(currentUser.UserId, id, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }
}
