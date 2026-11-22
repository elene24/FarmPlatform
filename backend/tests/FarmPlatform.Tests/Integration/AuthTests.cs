using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FarmPlatform.Tests.Integration;

public sealed class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SessionClient NewSession() => new(factory.CreateClient());

    [Fact]
    public async Task Successful_registration_signs_the_user_in_and_sends_a_verification_email()
    {
        var session = NewSession();
        var email = TestData.NewEmail();

        var response = await session.RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserBody>(Ct);
        Assert.Equal(email, user!.Email);
        Assert.Equal("+995555123456", user.PhoneNumber);
        Assert.False(user.EmailConfirmed);

        var me = await session.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains(factory.Emails.Messages, m => m.To == email && m.TextBody.Contains("/verify-email?", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Duplicate_email_registration_is_rejected_case_insensitively()
    {
        var email = TestData.NewEmail();
        await NewSession().RegisterAsync(email);

        var response = await NewSession().RegisterAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("auth.email_taken", await CodeOf(response));
    }

    [Fact]
    public async Task Invalid_registration_reports_every_field()
    {
        var response = await NewSession().PostAsync("/api/v1/auth/register", new
        {
            email = "not-an-email",
            password = "short",
            confirmPassword = "different",
            phoneNumber = "12",
            acceptTerms = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsOf(response);
        Assert.Equal(["validation.email_invalid"], errors["email"]);
        Assert.Equal(["validation.password_too_short"], errors["password"]);
        Assert.Equal(["validation.passwords_mismatch"], errors["confirmPassword"]);
        Assert.Equal(["validation.phone_invalid"], errors["phoneNumber"]);
        Assert.Equal(["validation.terms_required"], errors["acceptTerms"]);
    }

    [Fact]
    public async Task Successful_login_after_logout()
    {
        var email = TestData.NewEmail();
        var session = NewSession();
        await session.RegisterAsync(email);
        await session.PostAsync("/api/v1/auth/logout");

        var response = await session.LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Invalid_password_and_unknown_email_get_the_same_answer()
    {
        var email = TestData.NewEmail();
        await NewSession().RegisterAsync(email);

        var wrongPassword = await NewSession().LoginAsync(email, "not-the-password");
        var unknownEmail = await NewSession().LoginAsync(TestData.NewEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal("auth.invalid_credentials", await CodeOf(wrongPassword));
        Assert.Equal("auth.invalid_credentials", await CodeOf(unknownEmail));
    }

    [Fact]
    public async Task Account_locks_after_five_failed_logins()
    {
        var email = TestData.NewEmail();
        await NewSession().RegisterAsync(email);
        var attacker = NewSession();

        for (var i = 0; i < 5; i++)
        {
            await attacker.LoginAsync(email, "wrong-password");
        }

        var response = await attacker.LoginAsync(email); // correct password, but locked
        Assert.Equal((HttpStatusCode)423, response.StatusCode);
        Assert.Equal("auth.locked_out", await CodeOf(response));
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var session = NewSession();
        await session.RegisterAsync(TestData.NewEmail());

        var response = await session.PostAsync("/api/v1/auth/logout");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Email_verification_link_confirms_the_address()
    {
        var session = NewSession();
        var email = TestData.NewEmail();
        await session.RegisterAsync(email);
        var link = factory.Emails.LastLinkQuery(email, "/verify-email");

        var response = await session.PostAsync("/api/v1/auth/verify-email", new { userId = link["userId"], token = link["token"] });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var me = await (await session.GetAsync("/api/v1/auth/me")).Content.ReadFromJsonAsync<UserBody>(Ct);
        Assert.True(me!.EmailConfirmed);
    }

    [Fact]
    public async Task Tampered_verification_token_is_rejected()
    {
        var session = NewSession();
        var email = TestData.NewEmail();
        await session.RegisterAsync(email);
        var link = factory.Emails.LastLinkQuery(email, "/verify-email");

        var response = await session.PostAsync("/api/v1/auth/verify-email", new { userId = link["userId"], token = link["token"][..^4] + "AAAA" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("auth.invalid_token", await CodeOf(response));
    }

    [Fact]
    public async Task Password_reset_replaces_the_password_and_the_link_works_only_once()
    {
        var email = TestData.NewEmail();
        await NewSession().RegisterAsync(email);
        var session = NewSession();

        Assert.Equal(HttpStatusCode.Accepted, (await session.PostAsync("/api/v1/auth/forgot-password", new { email })).StatusCode);
        var link = factory.Emails.LastLinkQuery(email, "/reset-password");
        const string newPassword = "akhali-paroli-2026";

        var reset = await session.PostAsync("/api/v1/auth/reset-password", new { email, token = link["token"], newPassword, confirmPassword = newPassword });
        var reused = await session.PostAsync("/api/v1/auth/reset-password", new { email, token = link["token"], newPassword = "another-pass-2026", confirmPassword = "another-pass-2026" });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal("auth.invalid_token", await CodeOf(reused));
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewSession().LoginAsync(email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await NewSession().LoginAsync(email, newPassword)).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_whether_an_account_exists()
    {
        var before = factory.Emails.Messages.Count;

        var response = await NewSession().PostAsync("/api/v1/auth/forgot-password", new { email = TestData.NewEmail() });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(before, factory.Emails.Messages.Count);
    }

    [Theory]
    [InlineData("/api/v1/auth/me")]
    public async Task Unauthenticated_requests_cannot_access_protected_endpoints(string path)
    {
        var response = await NewSession().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resend_verification_requires_a_signed_in_user()
    {
        var response = await NewSession().PostAsync("/api/v1/auth/resend-verification");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task State_changing_requests_without_a_csrf_token_are_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { email = "a@b.ge", password = "x" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("security.csrf_invalid", await CodeOf(response));
    }

    [Fact]
    public async Task Csrf_token_issued_before_login_is_not_accepted_after_login()
    {
        var email = TestData.NewEmail();
        await NewSession().RegisterAsync(email);
        var client = factory.CreateClient();
        var token = (await client.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/auth/csrf", UriKind.Relative), Ct)).GetProperty("token").GetString()!;

        using var login = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/login", UriKind.Relative)) { Content = JsonContent.Create(new { email, password = TestData.Password }) };
        login.Headers.Add("X-XSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(login, Ct)).StatusCode);

        using var logout = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/auth/logout", UriKind.Relative));
        logout.Headers.Add("X-XSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(logout, Ct)).StatusCode);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static async Task<Dictionary<string, string[]>> ErrorsOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;

    private sealed record UserBody(Guid Id, string Email, bool EmailConfirmed, string? PhoneNumber, string PreferredLanguage);
}
