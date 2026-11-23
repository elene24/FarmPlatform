namespace FarmPlatform.Application.Common.Links;

/// <summary>Absolute links into the web app (used in SMS and email messages).</summary>
public interface IAppLinks
{
    /// <summary>Public page where a linked person agrees, declines or withdraws consent.</summary>
    Uri Consent(string token);
}
