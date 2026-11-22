using FarmPlatform.Application.Common.Email;
using FarmPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FarmPlatform.Tests.Integration;

/// <summary>
/// Hosts the real API in-process with an in-memory database. Good for HTTP pipeline tests
/// (auth, CSRF, routing). Tests that depend on PostgreSQL behaviour (unique indexes, check
/// constraints) will use a real PostgreSQL container in a later phase.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"api-tests-{Guid.NewGuid()}";

    /// <summary>Every email the app "sent" during this factory's lifetime.</summary>
    public CapturingEmailSender Emails { get; } = new();

    public ApiFactory()
    {
        // Non-development cookies are Secure-only ("__Host-" prefix), so talk to the test server over https.
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "Host=not-used");
        builder.UseSetting("Encryption:ActiveKeyId", "k1");
        builder.UseSetting("Encryption:Keys:k1", TestKeys.DataKey);
        builder.UseSetting("Encryption:BlindIndexKey", TestKeys.BlindIndexKey);
        builder.UseSetting("Frontend:BaseUrl", "https://app.test");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");

        builder.ConfigureServices(services =>
        {
            // Swap Npgsql for the in-memory provider.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    /// <summary>
    /// Creates the in-memory schema and HasData seed BEFORE the app starts. Startup services (e.g. the
    /// Data Protection key ring) write to the database, and EnsureCreated skips seeding if the store
    /// already exists by then.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var host = builder.Build();
        using (var scope = host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        }

        host.Start();
        return host;
    }
}
