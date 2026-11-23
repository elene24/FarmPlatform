using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FarmPlatform.Infrastructure.Identity;

namespace FarmPlatform.Tests.Integration;

/// <summary>Admin role (granted from the command line) and the read-only admin views.</summary>
public sealed class AdminTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task<(SessionClient Session, string Email)> VerifiedUserAsync()
    {
        var email = TestData.NewEmail();
        var session = new SessionClient(factory.CreateClient());
        (await session.RegisterAsync(email)).EnsureSuccessStatusCode();
        var link = factory.Emails.LastLinkQuery(email, "/verify-email");
        (await session.PostAsync("/api/v1/auth/verify-email", new { userId = link["userId"], token = link["token"] })).EnsureSuccessStatusCode();
        return (session, email);
    }

    /// <summary>Grants the role the way the make-admin command does.</summary>
    private async Task<SessionClient> AdminAsync()
    {
        var (session, email) = await VerifiedUserAsync();
        var (ok, _) = await AdminAccounts.SetAdminAsync(factory.Services, email, grant: true);
        Assert.True(ok);
        return session;
    }

    [Fact]
    public async Task Admin_endpoints_are_closed_to_anonymous_and_regular_users()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/stats", Ct)).StatusCode);

        var (user, _) = await VerifiedUserAsync();
        foreach (var path in new[] { "/api/v1/admin/stats", "/api/v1/admin/users", "/api/v1/admin/linked-pickers", $"/api/v1/admin/users/{Guid.NewGuid()}" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(path)).StatusCode);
        }

        var me = await Json(await user.GetAsync("/api/v1/auth/me"));
        Assert.False(me.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Make_admin_requires_an_existing_verified_account()
    {
        var (missing, _) = await AdminAccounts.SetAdminAsync(factory.Services, "nobody-" + TestData.NewEmail(), grant: true);
        Assert.False(missing);

        var email = TestData.NewEmail();
        await new SessionClient(factory.CreateClient()).RegisterAsync(email);
        var (unverified, message) = await AdminAccounts.SetAdminAsync(factory.Services, email, grant: true);
        Assert.False(unverified);
        Assert.Contains("verification", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_sees_users_by_role_with_masked_details()
    {
        var admin = await AdminAsync();
        Assert.True((await Json(await admin.GetAsync("/api/v1/auth/me"))).GetProperty("isAdmin").GetBoolean());

        // A picker with personal details, and a representative's linked picker.
        var picker = new SessionClient(factory.CreateClient());
        var pickerEmail = TestData.NewEmail();
        await picker.RegisterAsync(pickerEmail);
        var pin = TestData.NewPersonalId();
        (await picker.PostAsync("/api/v1/profiles/registration", new
        {
            picker = true,
            driver = false,
            orchardOwner = false,
            personalDetails = TestData.PersonalDetails(pin),
        })).EnsureSuccessStatusCode();
        var linkedPin = TestData.NewPersonalId();
        (await picker.PostAsync("/api/v1/linked-persons", new
        {
            firstName = "მზია", lastName = "ნოზაძე", personalIdNumber = linkedPin, phoneNumber = "+995577" + Random.Shared.Next(100000, 999999),
            legalAddress = "ქარელი", actualAddress = "ქარელი", latitude = 42.0, longitude = 43.9, payToRepresentative = true, bankAccountNumber = (string?)null,
        })).EnsureSuccessStatusCode();

        var pickers = await Json(await admin.GetAsync("/api/v1/admin/users?role=picker&pageSize=100"));
        var row = pickers.GetProperty("items").EnumerateArray().Single(u => u.GetProperty("email").GetString() == pickerEmail);
        Assert.True(row.GetProperty("picker").GetBoolean());
        Assert.False(row.GetProperty("driver").GetBoolean());
        Assert.Equal(1, row.GetProperty("linkedPickers").GetInt32());

        var drivers = await Json(await admin.GetAsync("/api/v1/admin/users?role=driver&pageSize=100"));
        Assert.DoesNotContain(drivers.GetProperty("items").EnumerateArray(), u => u.GetProperty("email").GetString() == pickerEmail);

        // Search by exact personal ID number finds the account through the blind index.
        var byPin = await Json(await admin.GetAsync($"/api/v1/admin/users?search={pin}"));
        Assert.Equal(pickerEmail, byPin.GetProperty("items")[0].GetProperty("email").GetString());

        var detailResponse = await admin.GetAsync($"/api/v1/admin/users/{row.GetProperty("id").GetString()}");
        var raw = await detailResponse.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.DoesNotContain(pin, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(linkedPin, raw, StringComparison.Ordinal);
        var detail = await Json(detailResponse);
        Assert.Equal(pin[^4..], detail.GetProperty("profiles").GetProperty("personalDetails").GetProperty("personalIdNumberLast4").GetString());
        Assert.Equal("მზია", detail.GetProperty("linkedPickers")[0].GetProperty("firstName").GetString());

        var linked = await Json(await admin.GetAsync("/api/v1/admin/linked-pickers?status=pending&search=ნოზაძე"));
        var linkedRow = linked.GetProperty("items").EnumerateArray().Single(l => l.GetProperty("personalIdNumberLast4").GetString() == linkedPin[^4..]);
        Assert.Equal(pickerEmail, linkedRow.GetProperty("representativeEmail").GetString());

        var stats = await Json(await admin.GetAsync("/api/v1/admin/stats"));
        Assert.True(stats.GetProperty("pickers").GetInt32() >= 1);
        Assert.True(stats.GetProperty("admins").GetInt32() >= 1);
    }

    [Fact]
    public async Task Invalid_filters_are_rejected_and_unknown_users_are_404()
    {
        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/users?role=boss")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/linked-pickers?status=maybe")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/users/{Guid.NewGuid()}")).StatusCode);

        // A search term with LIKE wildcards is treated literally.
        var all = await Json(await admin.GetAsync("/api/v1/admin/users?search=%25"));
        Assert.Equal(0, all.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Granting_and_removing_the_role_applies_to_the_current_session()
    {
        var (session, email) = await VerifiedUserAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await session.GetAsync("/api/v1/admin/stats")).StatusCode);

        await AdminAccounts.SetAdminAsync(factory.Services, email, grant: true);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/admin/stats")).StatusCode);

        var (ok, _) = await AdminAccounts.SetAdminAsync(factory.Services, email, grant: false);
        Assert.True(ok);
        Assert.Equal(HttpStatusCode.Forbidden, (await session.GetAsync("/api/v1/admin/stats")).StatusCode);
    }
}
