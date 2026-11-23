namespace FarmPlatform.Application.Common.Security;

/// <summary>Identity role names. Roles are separate from registration categories (picker, driver, orchard owner).</summary>
public static class AppRoles
{
    /// <summary>Can view every account and linked picker. Granted only with the make-admin command (see README).</summary>
    public const string Admin = "Admin";
}
