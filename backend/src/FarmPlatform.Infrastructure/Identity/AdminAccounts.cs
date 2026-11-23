using FarmPlatform.Application.Common.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FarmPlatform.Infrastructure.Identity;

/// <summary>
/// Grants or removes the Admin role from the command line (see README). There is deliberately no API for this:
/// only someone with access to the server and its database configuration can create an administrator.
/// </summary>
public static class AdminAccounts
{
    public static async Task<(bool Ok, string Message)> SetAdminAsync(IServiceProvider services, string email, bool grant)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return (false, $"No account with email {email}. Register it on the website first.");
        }

        if (grant && !user.EmailConfirmed)
        {
            return (false, $"{email} has not confirmed its email yet. Open the verification link first, then run this again.");
        }

        if (!await roles.RoleExistsAsync(AppRoles.Admin))
        {
            var created = await roles.CreateAsync(new IdentityRole<Guid>(AppRoles.Admin) { Id = Guid.CreateVersion7() });
            if (!created.Succeeded)
            {
                return (false, "Could not create the Admin role: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            }
        }

        var isAdmin = await users.IsInRoleAsync(user, AppRoles.Admin);
        if (grant == isAdmin)
        {
            return (true, grant ? $"{email} is already an admin." : $"{email} is not an admin.");
        }

        var result = grant ? await users.AddToRoleAsync(user, AppRoles.Admin) : await users.RemoveFromRoleAsync(user, AppRoles.Admin);
        if (!result.Succeeded)
        {
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // Takes effect immediately: admin requests check the role in the database (AdminRequirement).
        return (true, grant
            ? $"{email} is now an admin. Reload the website to see the admin panel."
            : $"{email} is no longer an admin.");
    }
}
