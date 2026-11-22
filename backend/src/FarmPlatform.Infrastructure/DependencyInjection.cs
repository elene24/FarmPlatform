using FarmPlatform.Application.Auth;
using FarmPlatform.Application.Common.Email;
using FarmPlatform.Application.Common.Persistence;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Infrastructure.Email;
using FarmPlatform.Infrastructure.Identity;
using FarmPlatform.Infrastructure.Persistence;
using FarmPlatform.Infrastructure.Persistence.Interceptors;
using FarmPlatform.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FarmPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditableEntityInterceptor>();

        // ---- Database (PostgreSQL / Neon) ----
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Default' is not configured.");

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                // Neon scales to zero; the first connection after idle can take a few seconds or fail transiently.
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            });
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // ---- ASP.NET Core Identity (cookie schemes are registered by the API project) ----
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Length over complexity rules (NIST SP 800-63B): easier for non-technical users, still strong.
                options.Password.RequiredLength = 10;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
                // Users are signed in right after registering; features that need a verified email use the
                // "EmailVerified" policy instead of blocking login (keeps the multi-step registration flowing).
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();

        // Re-check the security stamp every 10 minutes, so a password reset signs out other sessions quickly.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(10));

        services.AddOptions<FrontendOptions>()
            .Bind(configuration.GetSection(FrontendOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "Frontend:BaseUrl must be an absolute URL.")
            .ValidateOnStart();
        services.AddScoped<IAuthService, AuthService>();

        // ---- Email ----
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(o => o.Provider is "Log" or "Smtp", "Email:Provider must be 'Log' or 'Smtp'.")
            .Validate(o => o.Provider != "Smtp" || !string.IsNullOrWhiteSpace(o.Smtp.Host), "Email:Smtp:Host is required when Email:Provider is 'Smtp'.")
            .ValidateOnStart();
        if (string.Equals(configuration["Email:Provider"], "Smtp", StringComparison.Ordinal))
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        // Email confirmation / password reset links expire after 24h.
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(24));

        // Keys survive container restarts and are shared if Render ever runs more than one instance.
        services.AddDataProtection()
            .SetApplicationName("FarmPlatform")
            .PersistKeysToDbContext<AppDbContext>();

        // ---- Application-level encryption for personal ID / bank account numbers ----
        services.AddOptions<EncryptionOptions>()
            .Bind(configuration.GetSection(EncryptionOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ActiveKeyId) && o.Keys.ContainsKey(o.ActiveKeyId),
                "Encryption:ActiveKeyId and the matching Encryption:Keys entry are required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BlindIndexKey), "Encryption:BlindIndexKey is required.")
            .ValidateOnStart();
        services.AddSingleton<ISensitiveDataProtector, AesGcmSensitiveDataProtector>();

        return services;
    }
}
