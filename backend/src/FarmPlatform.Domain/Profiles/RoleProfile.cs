using FarmPlatform.Domain.Common;

namespace FarmPlatform.Domain.Profiles;

/// <summary>
/// A registration category held by a user (Picker, Driver, Orchard owner).
/// One row per user per category. Removing a category deactivates it instead of deleting the
/// account or its history; it can be reactivated later without re-entering data.
/// </summary>
public abstract class RoleProfile : Entity
{
    protected RoleProfile() { } // EF Core

    protected RoleProfile(Guid userId)
    {
        UserId = userId;
        IsActive = true;
    }

    public Guid UserId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public abstract ProfileCategory Category { get; }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DeactivatedAt = now;
    }

    public void Reactivate()
    {
        IsActive = true;
        DeactivatedAt = null;
    }
}
