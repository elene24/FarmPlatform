namespace FarmPlatform.Application.Auth;

// Request bodies are deserialized straight into these records. Properties are nullable on purpose:
// a missing JSON field arrives as null and is reported by AuthValidator with a translatable code.

public sealed record RegisterCommand(
    string? Email,
    string? Password,
    string? ConfirmPassword,
    string? PhoneNumber,
    bool AcceptTerms,
    string? PreferredLanguage);

public sealed record LoginCommand(string? Email, string? Password, bool RememberMe);

public sealed record VerifyEmailCommand(Guid UserId, string? Token);

public sealed record ForgotPasswordCommand(string? Email);

public sealed record ResetPasswordCommand(string? Email, string? Token, string? NewPassword, string? ConfirmPassword);

/// <summary>What the SPA needs to know about the signed-in user. Never contains sensitive personal data.</summary>
public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    string PreferredLanguage);
