using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FarmPlatform.Infrastructure.Identity;

/// <summary>
/// Adds an "email_verified" claim to the session cookie so authorization policies can require a verified
/// email without a database round trip. Refreshed on verification and by Identity's security-stamp check.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    public const string EmailVerifiedClaim = "email_verified";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(EmailVerifiedClaim, user.EmailConfirmed ? "true" : "false"));
        return identity;
    }
}
