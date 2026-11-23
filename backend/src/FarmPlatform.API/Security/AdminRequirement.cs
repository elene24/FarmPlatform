using System.Security.Claims;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FarmPlatform.Api.Security;

/// <summary>
/// The caller must hold the Admin role right now. Checked against the database on every admin request, not the
/// session cookie, so granting or removing the role takes effect immediately without logging in again.
/// </summary>
public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminRequirementHandler(UserManager<ApplicationUser> users) : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null)
        {
            return;
        }

        var user = await users.FindByIdAsync(id);
        if (user is { IsActive: true } && await users.IsInRoleAsync(user, AppRoles.Admin))
        {
            context.Succeed(requirement);
        }
    }
}
