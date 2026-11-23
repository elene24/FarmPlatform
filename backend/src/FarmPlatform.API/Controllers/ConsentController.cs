using FarmPlatform.Api.Errors;
using FarmPlatform.Api.Security;
using FarmPlatform.Application.Linked;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FarmPlatform.Api.Controllers;

/// <summary>
/// Public endpoints behind the link in a consent SMS. The token in the URL is the only credential: whoever has the
/// person's SMS can agree, decline or withdraw. Shows minimal information (first name, representative, payment).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/consent/{token}")]
[EnableRateLimiting(SecurityServiceCollectionExtensions.AuthRateLimitPolicy)]
public sealed class ConsentController(ILinkedPersonService linked) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ConsentRequestDto>> Get(string token, CancellationToken cancellationToken)
    {
        var request = await linked.GetConsentRequestAsync(token, cancellationToken);
        return request is null ? NotFound() : Ok(request);
    }

    [HttpPost("accept")]
    public Task<ActionResult<ConsentRequestDto>> Accept(string token, CancellationToken cancellationToken) =>
        RespondAsync(token, ConsentResponse.Accept, cancellationToken);

    [HttpPost("decline")]
    public Task<ActionResult<ConsentRequestDto>> Decline(string token, CancellationToken cancellationToken) =>
        RespondAsync(token, ConsentResponse.Decline, cancellationToken);

    [HttpPost("withdraw")]
    public Task<ActionResult<ConsentRequestDto>> Withdraw(string token, CancellationToken cancellationToken) =>
        RespondAsync(token, ConsentResponse.Withdraw, cancellationToken);

    private async Task<ActionResult<ConsentRequestDto>> RespondAsync(string token, ConsentResponse response, CancellationToken cancellationToken)
    {
        var result = await linked.RespondAsync(token, response, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }
}
