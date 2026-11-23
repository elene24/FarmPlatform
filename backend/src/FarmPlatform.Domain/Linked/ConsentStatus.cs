namespace FarmPlatform.Domain.Linked;

/// <summary>Whether a linked person has agreed to be represented.</summary>
public enum ConsentStatus
{
    /// <summary>SMS sent, no answer yet. The person cannot be offered work.</summary>
    Pending = 1,
    Confirmed = 2,
    Declined = 3,
    /// <summary>The person agreed earlier and later withdrew consent.</summary>
    Revoked = 4,
}

/// <summary>How consent was given.</summary>
public enum ConsentMethod
{
    /// <summary>The person told the representative the code from the SMS, who entered it.</summary>
    Code = 1,
    /// <summary>The person opened the link from the SMS and pressed "Agree".</summary>
    Link = 2,
}
