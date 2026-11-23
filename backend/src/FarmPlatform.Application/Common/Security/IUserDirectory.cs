namespace FarmPlatform.Application.Common.Security;

/// <summary>Read-only facts about accounts that live in ASP.NET Core Identity.</summary>
public interface IUserDirectory
{
    /// <summary>The account's main phone number (E.164), or null.</summary>
    Task<string?> GetPhoneNumberAsync(Guid userId, CancellationToken cancellationToken);
}
