using FarmPlatform.Api.Errors;
using FarmPlatform.Api.Security;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Application.Linked;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FarmPlatform.Api.Controllers;

/// <summary>
/// Pickers without an account, managed by the signed-in user as their representative.
/// Another representative's records answer 404.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/linked-persons")]
public sealed class LinkedPersonsController(ILinkedPersonService linked, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LinkedPersonDto>>> List(CancellationToken cancellationToken) =>
        Ok(await linked.ListAsync(currentUser.UserId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LinkedPersonDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var person = await linked.GetAsync(currentUser.UserId, id, cancellationToken);
        return person is null ? NotFound() : Ok(person);
    }

    /// <summary>Adds a person and sends them a consent SMS. They stay "pending" until they agree.</summary>
    [HttpPost]
    public async Task<ActionResult<LinkedPersonDto>> Create(LinkedPersonInput input, CancellationToken cancellationToken)
    {
        var result = await linked.CreateAsync(currentUser.UserId, input, cancellationToken);
        return result.IsSuccess ? Created($"/api/v1/linked-persons/{result.Value!.Id}", result.Value) : this.ToProblem(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LinkedPersonDto>> Update(Guid id, LinkedPersonInput input, CancellationToken cancellationToken)
    {
        var result = await linked.UpdateAsync(currentUser.UserId, id, input, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }

    /// <summary>Ends the representation and deletes the person's data.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await linked.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }

    [HttpPost("{id:guid}/resend-consent")]
    public async Task<ActionResult<LinkedPersonDto>> ResendConsent(Guid id, CancellationToken cancellationToken)
    {
        var result = await linked.ResendConsentAsync(currentUser.UserId, id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }

    /// <summary>The code the person read out from their SMS. Limited attempts per SMS, and rate limited.</summary>
    [HttpPost("{id:guid}/confirm")]
    [EnableRateLimiting(SecurityServiceCollectionExtensions.AuthRateLimitPolicy)]
    public async Task<ActionResult<LinkedPersonDto>> Confirm(Guid id, ConsentCodeInput input, CancellationToken cancellationToken)
    {
        var result = await linked.ConfirmWithCodeAsync(currentUser.UserId, id, input?.Code, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }
}
