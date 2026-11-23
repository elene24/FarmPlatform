using FarmPlatform.Application.Common.Links;
using Microsoft.Extensions.Options;

namespace FarmPlatform.Infrastructure.Identity;

public sealed class AppLinks(IOptions<FrontendOptions> options) : IAppLinks
{
    /// <summary>Short path ("/c/...") keeps the SMS short.</summary>
    public Uri Consent(string token) => new($"{options.Value.BaseUrl.TrimEnd('/')}/c/{Uri.EscapeDataString(token)}");
}
