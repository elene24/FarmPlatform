using FarmPlatform.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace FarmPlatform.Infrastructure.Identity;

/// <summary>
/// The authenticated account. Email, phone, password hash, lockout and security stamp are managed by
/// ASP.NET Core Identity. One account can hold any number of registration categories; profile tables
/// reference this row by UserId and never duplicate authentication data.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>, IAuditable
{
    public const int ProfilePhotoKeyMaxLength = 200;

    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    /// <summary>Object-storage key of the profile photo in R2 (not a URL, not the file name the user uploaded).</summary>
    public string? ProfilePhotoKey { get; set; }

    /// <summary>UI language: "ka" (default) or "en". Also used for emails.</summary>
    public string PreferredLanguage { get; set; } = "ka";

    /// <summary>False when an administrator disables the account. Distinct from Identity lockout.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When the user accepted the terms and conditions, and which version (see LegalDocuments).</summary>
    public DateTimeOffset? TermsAcceptedAt { get; set; }

    public string? TermsVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
