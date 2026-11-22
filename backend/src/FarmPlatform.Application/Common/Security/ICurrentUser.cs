namespace FarmPlatform.Application.Common.Security;

/// <summary>
/// The authenticated caller. Ownership checks always use this id, never an id taken from the
/// request body or route, so a user cannot reach another user's data by changing an id.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The signed-in user's id. Throws if the request is anonymous.</summary>
    Guid UserId { get; }
}
