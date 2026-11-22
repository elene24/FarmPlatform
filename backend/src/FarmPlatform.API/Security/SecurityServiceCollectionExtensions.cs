using System.Threading.RateLimiting;
using FarmPlatform.Application.Common.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace FarmPlatform.Api.Security;

public static class SecurityServiceCollectionExtensions
{
    public const string FrontendCorsPolicy = "Frontend";
    public const string AuthRateLimitPolicy = "auth";
    public const string CsrfHeaderName = "X-XSRF-TOKEN";
    public const string EmailVerifiedPolicy = "EmailVerified";

    /// <summary>
    /// Cookie authentication (ASP.NET Core Identity), CSRF tokens, CORS and rate limiting.
    ///
    /// Deployment model this assumes: frontend on https://example.ge (Cloudflare Pages) and API on
    /// https://api.example.ge (Render). Both are the same *site*, so a SameSite=Lax session cookie set by
    /// the API is sent on the SPA's fetch(..., { credentials: "include" }) calls. In development the Vite
    /// dev server proxies /api to the backend, so everything is same-origin.
    /// </summary>
    public static IServiceCollection AddPlatformSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var isDevelopment = environment.IsDevelopment();

        // "__Host-" cookies must be Secure, Path=/ and have no Domain: the browser then guarantees they
        // can only have been set by this exact host over HTTPS. Plain HTTP localhost can't use the prefix.
        var cookiePrefix = isDevelopment ? string.Empty : "__Host-";
        var securePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = $"{cookiePrefix}fp.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = securePolicy;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // This is a JSON API: answer 401/403 instead of redirecting to a login page.
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
                OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                },
            };
        });

        services.AddAuthorizationBuilder()
            // For features that must only be used with a confirmed email (Phase 3+).
            .AddPolicy(EmailVerifiedPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(Infrastructure.Identity.AppUserClaimsPrincipalFactory.EmailVerifiedClaim, "true"));

        // CSRF: synchronizer token. The SPA calls GET /api/v1/auth/csrf, keeps the token in memory and sends it
        // in X-XSRF-TOKEN on every unsafe request; CsrfValidationFilter checks it against this cookie.
        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfHeaderName;
            options.Cookie.Name = $"{cookiePrefix}fp.csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = securePolicy;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowCredentials()
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Content-Type", "Accept", "Accept-Language", CsrfHeaderName)
            .SetPreflightMaxAge(TimeSpan.FromHours(1))));

        // Brute-force protection for login/register/password-reset: 10 requests per minute per client IP
        // (RateLimiting:AuthPermitLimit). On Render, ASPNETCORE_FORWARDEDHEADERS_ENABLED=true (set in the
        // Dockerfile) makes RemoteIpAddress the real client IP.
        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }

    /// <summary>Conservative security headers for a JSON API.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            await next(context);
        });
}
