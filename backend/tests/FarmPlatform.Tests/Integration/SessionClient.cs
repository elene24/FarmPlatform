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

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> PutAsync(string path, object? body) => SendAsync(HttpMethod.Put, path, body);

    public Task<HttpResponseMessage> DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path, null);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        _csrf ??= await FetchCsrfAsync();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
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

    /// <summary>A random, well-formed 11-digit Georgian personal number.</summary>
    public static string NewPersonalId() =>
        string.Concat(Enumerable.Range(0, 11).Select(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(10)));

    /// <summary>A random Georgian IBAN with valid check digits (GE + 2 + "TB" + 16 digits).</summary>
    public static string NewIban()
    {
        var account = string.Concat(Enumerable.Range(0, 16).Select(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(10)));
        var numeric = "2911" + account + "1614" + "00"; // "TB" = 29 11, "GE" = 16 14
        var check = 98 - (int)(System.Numerics.BigInteger.Parse(numeric, System.Globalization.CultureInfo.InvariantCulture) % 97);
        return $"GE{check:00}TB{account}";
    }

    public static object PersonalDetails(string? personalId = null, string? iban = null) => new
    {
        firstName = "ნინო",
        lastName = "ბერიძე",
        personalIdNumber = personalId ?? NewPersonalId(),
        bankAccountNumber = iban ?? NewIban(),
        legalAddress = "თბილისი, რუსთაველის გამზ. 1",
        actualAddress = "გორი, სტალინის ქ. 5",
        latitude = 41.984m,
        longitude = 44.110m,
    };

    public static object Vehicle(string plate = "AA-123-BB", int capacity = 14) =>
        new { licensePlateNumber = plate, model = "Ford Transit", passengerCapacity = capacity };

    public static object Orchard(string name = "ვაშლის ბაღი", int statusId = 2, string idNumber = "404123456", decimal area = 12.5m) => new
    {
        legalEntityStatusId = statusId,
        name,
        identificationNumber = idNumber,
        address = "ქარელი, სოფ. აგარა",
        latitude = 42.02m,
        longitude = 43.89m,
        areaHectares = area,
    };
}
