using System.Net;
using System.Net.Http.Json;

namespace FarmPlatform.Tests.Integration;

public sealed class ApiSmokeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Liveness_endpoint_is_healthy()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/health/live", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_endpoint_checks_the_database()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_requires_authentication_and_returns_401_not_a_redirect()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Csrf_endpoint_issues_a_token_and_an_httponly_cookie()
    {
        // HandleCookies=false so the raw Set-Cookie header stays visible to the assertion.
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        var response = await client.GetAsync(new Uri("/api/v1/auth/csrf", UriKind.Relative), Ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TokenBody>(Ct);
        Assert.False(string.IsNullOrWhiteSpace(body?.Token));

        var setCookie = string.Join(';', response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Legal_entity_statuses_are_served_from_the_database()
    {
        var items = await factory.CreateClient()
            .GetFromJsonAsync<List<StatusBody>>(new Uri("/api/v1/lookups/legal-entity-statuses", UriKind.Relative), Ct);

        Assert.NotNull(items);
        Assert.Equal(new[] { "individual_entrepreneur", "llc", "natural_person", "other" }, items.Select(i => i.Code));
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/health/live", UriKind.Relative), Ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    private sealed record TokenBody(string Token);

    private sealed record StatusBody(int Id, string Code, string NameKa, string NameEn);
}
