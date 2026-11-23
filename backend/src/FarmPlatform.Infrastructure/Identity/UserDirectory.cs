using FarmPlatform.Application.Common.Security;
using Microsoft.AspNetCore.Identity;

namespace FarmPlatform.Infrastructure.Identity;

public sealed class UserDirectory(UserManager<ApplicationUser> userManager) : IUserDirectory
{
    public async Task<string?> GetPhoneNumberAsync(Guid userId, CancellationToken cancellationToken) =>
        (await userManager.FindByIdAsync(userId.ToString()))?.PhoneNumber;
}
