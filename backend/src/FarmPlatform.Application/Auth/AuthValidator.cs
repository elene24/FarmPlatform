using System.Net.Mail;
using FarmPlatform.Application.Common.Results;

namespace FarmPlatform.Application.Auth;

/// <summary>
/// Input validation for auth requests. Returns stable error codes (translated by the frontend), so the same
/// rules give the same messages in Georgian and English. Mirrors the client-side Zod schemas.
/// </summary>
public static class AuthValidator
{
    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 128;
    public const int EmailMaxLength = 254;
    public static readonly IReadOnlySet<string> SupportedLanguages = new HashSet<string>(StringComparer.Ordinal) { "ka", "en" };

    public static List<AppError> Validate(RegisterCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new List<AppError>();
        ValidateEmail(command.Email, errors);
        ValidateNewPassword(command.Password, command.ConfirmPassword, "password", errors);

        if (!PhoneNumberNormalizer.TryNormalize(command.PhoneNumber, out _))
        {
            errors.Add(new AppError(string.IsNullOrWhiteSpace(command.PhoneNumber) ? "validation.required" : "validation.phone_invalid", "phoneNumber"));
        }

        if (!command.AcceptTerms)
        {
            errors.Add(new AppError("validation.terms_required", "acceptTerms"));
        }

        if (command.PreferredLanguage is not null && !SupportedLanguages.Contains(command.PreferredLanguage))
        {
            errors.Add(new AppError("validation.language_invalid", "preferredLanguage"));
        }

        return errors;
    }

    public static List<AppError> Validate(LoginCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new List<AppError>();
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            errors.Add(new AppError("validation.required", "email"));
        }

        if (string.IsNullOrEmpty(command.Password))
        {
            errors.Add(new AppError("validation.required", "password"));
        }

        return errors;
    }

    public static List<AppError> Validate(ForgotPasswordCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new List<AppError>();
        ValidateEmail(command.Email, errors);
        return errors;
    }

    public static List<AppError> Validate(ResetPasswordCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new List<AppError>();
        ValidateEmail(command.Email, errors);
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            errors.Add(new AppError("auth.invalid_token", "token"));
        }

        ValidateNewPassword(command.NewPassword, command.ConfirmPassword, "newPassword", errors);
        return errors;
    }

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailMaxLength)
        {
            return false;
        }

        var trimmed = email.Trim();
        // MailAddress accepts display names ("Bob <b@x.ge>"); require the bare address and a dot in the domain.
        return MailAddress.TryCreate(trimmed, out var parsed)
            && parsed.Address == trimmed
            && parsed.Host.Contains('.', StringComparison.Ordinal)
            && !parsed.Host.EndsWith('.');
    }

    private static void ValidateEmail(string? email, List<AppError> errors)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add(new AppError("validation.required", "email"));
        }
        else if (!IsValidEmail(email))
        {
            errors.Add(new AppError("validation.email_invalid", "email"));
        }
    }

    private static void ValidateNewPassword(string? password, string? confirm, string field, List<AppError> errors)
    {
        if (string.IsNullOrEmpty(password))
        {
            errors.Add(new AppError("validation.required", field));
            return;
        }

        if (password.Length < PasswordMinLength)
        {
            errors.Add(new AppError("validation.password_too_short", field));
        }
        else if (password.Length > PasswordMaxLength)
        {
            errors.Add(new AppError("validation.password_too_long", field));
        }

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            errors.Add(new AppError("validation.passwords_mismatch", "confirmPassword"));
        }
    }
}
