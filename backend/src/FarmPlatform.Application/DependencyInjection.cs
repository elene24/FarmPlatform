using FarmPlatform.Application.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace FarmPlatform.Application;

public static class DependencyInjection
{
    /// <summary>Use cases that live in the Application layer (auth is implemented in Infrastructure on Identity).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
