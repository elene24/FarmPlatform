using System.Net.Http.Json;

namespace FarmPlatform.Tests.Integration;

/// <summary>
/// Behaves like the SPA: keeps cookies, fetches a CSRF token and sends it on every unsafe request,
/// and refreshes the token after the signed-in user changes.
/// </summary>
public sealed class SessionClient(HttpClient http)
{
    private string? _csrf;

    public HttpClient Http { get; } = http;

    public Task<HttpResponseMessage> GetAsync(string path) =>
        Http.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

    public async Task<HttpResponseMessage> PostAsync(string path, object? body = null)
    {
        _csrf ??= await FetchCsrfAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Add("X-XSRF-TOKEN", _csrf);
        var response = await Http.SendAsync(request, TestContext.Current.CancellationToken);

        if (path.EndsWith("/login", StringComparison.Ordinal) || path.EndsWith("/register", StringComparison.Ordinal) || path.EndsWith("/logout", StringComparison.Ordinal))
        {
            _csrf = null; // the token is bound to the user, who just changed
        }

        return response;
    }

    public async Task<HttpResponseMessage> RegisterAsync(string email, string password = TestData.Password, string phone = "555 12 34 56") =>
        await PostAsync("/api/v1/auth/register", new
        {
            email,
            password,
            confirmPassword = password,
            phoneNumber = phone,
            acceptTerms = true,
            preferredLanguage = "en",
        });

    public Task<HttpResponseMessage> LoginAsync(string email, string password = TestData.Password) =>
        PostAsync("/api/v1/auth/login", new { email, password, rememberMe = false });

    private async Task<string> FetchCsrfAsync()
    {
        var response = await Http.GetFromJsonAsync<CsrfBody>(new Uri("/api/v1/auth/csrf", UriKind.Relative), TestContext.Current.CancellationToken);
        return response!.Token;
    }

    private sealed record CsrfBody(string Token);
}

public static class TestData
{
    public const string Password = "mtsvane-vashli-2026";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.ge";
}
