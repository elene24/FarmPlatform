namespace FarmPlatform.Infrastructure.Identity;

/// <summary>Where the SPA lives; used to build links in emails (verification, password reset).</summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>e.g. http://localhost:5173 in development, https://example.ge in production.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
