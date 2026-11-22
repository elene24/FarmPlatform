using FarmPlatform.Api.Errors;
using FarmPlatform.Api.Security;
using FarmPlatform.Application.Auth;
using FarmPlatform.Application.Common.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FarmPlatform.Api.Controllers;

/// <summary>
/// Registration, login/logout, email verification and password reset.
/// All POSTs require the X-XSRF-TOKEN header (see CsrfValidationFilter) and are rate limited per client IP.
/// After login, register or logout the SPA must fetch a new CSRF token: tokens are bound to the signed-in user.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting(SecurityServiceCollectionExtensions.AuthRateLimitPolicy)]
public sealed class AuthController(IAuthService auth, IAntiforgery antiforgery, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Issues a CSRF token (and its paired cookie). Call on app start and after login/register/logout.</summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public ActionResult<CsrfTokenResponse> GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken!));
    }

    /// <summary>Creates an account, emails a verification link and signs the user in.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CurrentUserDto>> Register(RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(command, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetMe), result.Value) : this.ToProblem(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUserDto>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }

    /// <summary>Ends the session. Safe to call when already signed out.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public async Task<IActionResult> Logout()
    {
        await auth.LogoutAsync();
        return NoContent();
    }

    /// <summary>Confirms the email address using the link from the verification email.</summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        Guid? signedIn = currentUser.IsAuthenticated ? currentUser.UserId : null;
        var result = await auth.VerifyEmailAsync(command, signedIn, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }

    /// <summary>Sends the verification email again to the signed-in user.</summary>
    [HttpPost("resend-verification")]
    [Authorize]
    public async Task<IActionResult> ResendVerification(CancellationToken cancellationToken)
    {
        var result = await auth.ResendVerificationEmailAsync(currentUser.UserId, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }

    /// <summary>Emails a reset link if the account exists. The response is the same either way.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var result = await auth.ForgotPasswordAsync(command, cancellationToken);
        return result.IsSuccess ? Accepted() : this.ToProblem(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var result = await auth.ResetPasswordAsync(command, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }

    /// <summary>The signed-in user, or 401.</summary>
    [HttpGet("me")]
    [Authorize]
    [DisableRateLimiting]
    public async Task<ActionResult<CurrentUserDto>> GetMe(CancellationToken cancellationToken)
    {
        var user = await auth.GetCurrentUserAsync(currentUser.UserId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }
}

public sealed record CsrfTokenResponse(string Token);
