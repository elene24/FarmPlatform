using FarmPlatform.Application.Common.Results;

namespace FarmPlatform.Application.Auth;

/// <summary>
/// Account registration and authentication. Implemented in Infrastructure on top of ASP.NET Core Identity
/// (UserManager/SignInManager), which also issues the session cookie.
/// </summary>
public interface IAuthService
{
    /// <summary>Creates the account, sends the verification email and signs the user in.</summary>
    Task<Result<CurrentUserDto>> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken);

    Task<Result<CurrentUserDto>> LoginAsync(LoginCommand command, CancellationToken cancellationToken);

    Task LogoutAsync();

    /// <param name="currentUserId">The signed-in user, if any; their session is refreshed when they verify their own email.</param>
    Task<Result> VerifyEmailAsync(VerifyEmailCommand command, Guid? currentUserId, CancellationToken cancellationToken);

    Task<Result> ResendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Always succeeds for a well-formed email, whether or not an account exists.</summary>
    Task<Result> ForgotPasswordAsync(ForgotPasswordCommand command, CancellationToken cancellationToken);

    Task<Result> ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken);

    Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
