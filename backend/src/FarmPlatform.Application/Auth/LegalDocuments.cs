namespace FarmPlatform.Application.Auth;

public static class LegalDocuments
{
    /// <summary>
    /// Version of the terms and conditions users accept at registration. Stored with the account so we can
    /// prove which version someone agreed to; bump it when the terms change.
    /// </summary>
    public const string CurrentTermsVersion = "2026-11-22";
}
