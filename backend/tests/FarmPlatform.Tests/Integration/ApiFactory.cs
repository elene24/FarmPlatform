using FarmPlatform.Application.Common.Email;
using FarmPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace FarmPlatform.Tests.Integration;

/// <summary>
/// Hosts the real API in-process against a real, throwaway PostgreSQL database (one per test class), created by
/// applying the actual EF Core migrations and dropped afterwards. Uses the docker compose database by default
/// (127.0.0.1:15432); override with the FARMPLATFORM_TEST_DB environment variable (without a Database= part).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string DefaultServer = "Host=127.0.0.1;Port=15432;Username=farmplatform;Password=farmplatform_dev_pw";

    private readonly string _connectionString = new NpgsqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("FARMPLATFORM_TEST_DB") ?? DefaultServer)
    {
        Database = $"farmplatform_test_{Guid.NewGuid():N}",
    }.ConnectionString;

    public ApiFactory()
    {
        // Non-development cookies are Secure-only ("__Host-" prefix), so talk to the test server over https.
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    /// <summary>Every email the app "sent" during this factory's lifetime.</summary>
    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Encryption:ActiveKeyId", "k1");
        builder.UseSetting("Encryption:Keys:k1", TestKeys.DataKey);
        builder.UseSetting("Encryption:BlindIndexKey", TestKeys.BlindIndexKey);
        builder.UseSetting("Frontend:BaseUrl", "https://app.test");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    /// <summary>Applies the migrations (schema + seed data) before the app starts.</summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var host = builder.Build();
        using (var scope = host.Services.CreateScope())
        {
            try
            {
                scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
            }
            catch (NpgsqlException ex)
            {
                throw new InvalidOperationException(
                    "Integration tests need PostgreSQL. Start it with 'docker compose up -d postgres' or set FARMPLATFORM_TEST_DB.", ex);
            }
        }

        host.Start();
        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
        catch (InvalidOperationException)
        {
            // Host never started (e.g. no database): nothing to clean up.
        }

        await base.DisposeAsync();
    }
}
