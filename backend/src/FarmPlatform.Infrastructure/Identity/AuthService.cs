using System.Text;
using FarmPlatform.Application.Auth;
using FarmPlatform.Application.Common.Email;
using FarmPlatform.Application.Common.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FarmPlatform.Infrastructure.Identity;

public sealed partial class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result<CurrentUserDto>> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = AuthValidator.Validate(command);
        if (errors.Count > 0)
        {
            return Result.Failure<CurrentUserDto>(ErrorKind.Validation, [.. errors]);
        }

        var email = command.Email!.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Result.Failure<CurrentUserDto>(ErrorKind.Conflict, new AppError("auth.email_taken", "email"));
        }

        PhoneNumberNormalizer.TryNormalize(command.PhoneNumber, out var phone);
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = phone,
            PreferredLanguage = command.PreferredLanguage ?? "ka",
            TermsAcceptedAt = timeProvider.GetUtcNow(),
            TermsVersion = LegalDocuments.CurrentTermsVersion,
        };

        var created = await userManager.CreateAsync(user, command.Password!);
        if (!created.Succeeded)
        {
            return MapIdentityErrors<CurrentUserDto>(created, passwordField: "password");
        }

        LogRegistered(logger, user.Id);
        await SendVerificationEmailAsync(user, cancellationToken);
        await signInManager.SignInAsync(user, isPersistent: true);

        return Result.Success(ToDto(user));
    }

    public async Task<Result<CurrentUserDto>> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = AuthValidator.Validate(command);
        if (errors.Count > 0)
        {
            return Result.Failure<CurrentUserDto>(ErrorKind.Validation, [.. errors]);
        }

        var user = await userManager.FindByEmailAsync(command.Email!.Trim());
        if (user is null)
        {
            return Result.Failure<CurrentUserDto>(ErrorKind.Unauthorized, new AppError("auth.invalid_credentials"));
        }

        if (!user.IsActive)
        {
            return Result.Failure<CurrentUserDto>(ErrorKind.Forbidden, new AppError("auth.account_disabled"));
        }

        var result = await signInManager.PasswordSignInAsync(user, command.Password!, command.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return Result.Success(ToDto(user));
        }

        if (result.IsLockedOut)
        {
            LogLockedOut(logger, user.Id);
            return Result.Failure<CurrentUserDto>(ErrorKind.Locked, new AppError("auth.locked_out"));
        }

        return Result.Failure<CurrentUserDto>(ErrorKind.Unauthorized, new AppError("auth.invalid_credentials"));
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    public async Task<Result> VerifyEmailAsync(VerifyEmailCommand command, Guid? currentUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await userManager.FindByIdAsync(command.UserId.ToString());
        var token = DecodeToken(command.Token);
        if (user is null || token is null)
        {
            return Result.Failure(ErrorKind.Validation, new AppError("auth.invalid_token", "token"));
        }

        if (!user.EmailConfirmed)
        {
            var confirmed = await userManager.ConfirmEmailAsync(user, token);
            if (!confirmed.Succeeded)
            {
                return Result.Failure(ErrorKind.Validation, new AppError("auth.invalid_token", "token"));
            }
        }

        if (currentUserId == user.Id)
        {
            await signInManager.RefreshSignInAsync(user); // update the email_verified claim in the cookie
        }

        return Result.Success();
    }

    public async Task<Result> ResendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure(ErrorKind.NotFound, new AppError("auth.user_not_found"));
        }

        if (!user.EmailConfirmed)
        {
            await SendVerificationEmailAsync(user, cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = AuthValidator.Validate(command);
        if (errors.Count > 0)
        {
            return Result.Failure(ErrorKind.Validation, [.. errors]);
        }

        var user = await userManager.FindByEmailAsync(command.Email!.Trim());
        if (user is null || !user.IsActive)
        {
            return Result.Success(); // same response either way
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = BuildLink("/reset-password", new Dictionary<string, string?>
        {
            ["email"] = user.Email,
            ["token"] = EncodeToken(token),
        });

        await SendSafelyAsync(AuthEmailTemplates.ResetPassword(user.Email!, user.PreferredLanguage, link), cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = AuthValidator.Validate(command);
        if (errors.Count > 0)
        {
            return Result.Failure(ErrorKind.Validation, [.. errors]);
        }

        var user = await userManager.FindByEmailAsync(command.Email!.Trim());
        var token = DecodeToken(command.Token);
        if (user is null || token is null)
        {
            return Result.Failure(ErrorKind.Validation, new AppError("auth.invalid_token", "token"));
        }

        var reset = await userManager.ResetPasswordAsync(user, token, command.NewPassword!);
        if (!reset.Succeeded)
        {
            return MapIdentityErrors<bool>(reset, passwordField: "newPassword");
        }

        // The reset link proved the user controls the mailbox, and they should be able to log in right away.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        LogPasswordReset(logger, user.Id);
        return Result.Success();
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null || !user.IsActive ? null : ToDto(user);
    }

    private async Task SendVerificationEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildLink("/verify-email", new Dictionary<string, string?>
        {
            ["userId"] = user.Id.ToString(),
            ["token"] = EncodeToken(token),
        });

        await SendSafelyAsync(AuthEmailTemplates.VerifyEmail(user.Email!, user.PreferredLanguage, link), cancellationToken);
    }

    /// <summary>Email delivery problems must not fail registration or reveal anything; they are logged instead.</summary>
    private async Task SendSafelyAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
#pragma warning disable CA1031 // Any transport failure is logged and swallowed by design.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogEmailFailed(logger, ex, message.Subject);
        }
    }

    private Uri BuildLink(string path, Dictionary<string, string?> query)
    {
        var baseUrl = frontendOptions.Value.BaseUrl.TrimEnd('/');
        return new Uri(QueryHelpers.AddQueryString(baseUrl + path, query));
    }

    // Identity tokens contain '+', '/' and '='; Base64Url keeps them intact inside a URL.
    private static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string? DecodeToken(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Result<T> MapIdentityErrors<T>(IdentityResult result, string passwordField)
    {
        var mapped = new List<AppError>();
        var conflict = false;
        foreach (var error in result.Errors)
        {
            switch (error.Code)
            {
                case nameof(IdentityErrorDescriber.DuplicateEmail):
                case nameof(IdentityErrorDescriber.DuplicateUserName):
                    conflict = true;
                    mapped.Add(new AppError("auth.email_taken", "email"));
                    break;
                case nameof(IdentityErrorDescriber.InvalidToken):
                    mapped.Add(new AppError("auth.invalid_token", "token"));
                    break;
                case nameof(IdentityErrorDescriber.PasswordTooShort):
                    mapped.Add(new AppError("validation.password_too_short", passwordField));
                    break;
                case nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars):
                    mapped.Add(new AppError("validation.password_weak", passwordField));
                    break;
                case nameof(IdentityErrorDescriber.InvalidEmail):
                case nameof(IdentityErrorDescriber.InvalidUserName):
                    mapped.Add(new AppError("validation.email_invalid", "email"));
                    break;
                default:
                    mapped.Add(new AppError(error.Code.StartsWith("Password", StringComparison.Ordinal) ? "validation.password_weak" : "validation.invalid", passwordField));
                    break;
            }
        }

        return Result.Failure<T>(conflict ? ErrorKind.Conflict : ErrorKind.Validation, [.. mapped.Distinct()]);
    }

    private static CurrentUserDto ToDto(ApplicationUser user) =>
        new(user.Id, user.Email!, user.EmailConfirmed, user.PhoneNumber, user.PreferredLanguage);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} registered")]
    private static partial void LogRegistered(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} locked out after repeated failed logins")]
    private static partial void LogLockedOut(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} reset their password")]
    private static partial void LogPasswordReset(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email '{Subject}'")]
    private static partial void LogEmailFailed(ILogger logger, Exception exception, string subject);
}
