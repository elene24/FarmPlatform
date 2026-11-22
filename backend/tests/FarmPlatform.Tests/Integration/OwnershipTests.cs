using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FarmPlatform.Tests.Integration;

/// <summary>Changing an id in the URL must never reach another user's data (spec §13, §19).</summary>
public sealed class OwnershipTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<SessionClient> SignedInAsync()
    {
        var session = new SessionClient(factory.CreateClient());
        (await session.RegisterAsync(TestData.NewEmail())).EnsureSuccessStatusCode();
        return session;
    }

    private static async Task<string> FirstIdAsync(SessionClient s, string path) =>
        (await (await s.GetAsync(path)).Content.ReadFromJsonAsync<JsonElement>(Ct))[0].GetProperty("id").GetString()!;

    [Fact]
    public async Task User_cannot_modify_another_users_vehicle()
    {
        var owner = await SignedInAsync();
        await owner.PostAsync("/api/v1/profiles/registration", new { driver = true, personalDetails = TestData.PersonalDetails(), vehicles = new[] { TestData.Vehicle("OW-111-NR", 10) } });
        var vehicleId = await FirstIdAsync(owner, "/api/v1/vehicles");

        var attacker = await SignedInAsync();
        await attacker.PostAsync("/api/v1/profiles/registration", new { driver = true, personalDetails = TestData.PersonalDetails(), vehicles = new[] { TestData.Vehicle("AT-222-KR", 4) } });

        var update = await attacker.PutAsync($"/api/v1/vehicles/{vehicleId}", TestData.Vehicle("HACKED1", 60));
        var delete = await attacker.DeleteAsync($"/api/v1/vehicles/{vehicleId}");

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        var stillThere = await (await owner.GetAsync("/api/v1/vehicles")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("OW111NR", stillThere[0].GetProperty("licensePlateNumber").GetString());
        Assert.Equal(10, stillThere[0].GetProperty("passengerCapacity").GetInt32());
        Assert.DoesNotContain("OW111NR", await (await attacker.GetAsync("/api/v1/vehicles")).Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task User_cannot_read_or_edit_another_users_orchard()
    {
        var owner = await SignedInAsync();
        await owner.PostAsync("/api/v1/profiles/registration", new { orchardOwner = true, orchards = new[] { TestData.Orchard("Owner's orchard") } });
        var orchardId = await FirstIdAsync(owner, "/api/v1/orchards");

        var attacker = await SignedInAsync();
        await attacker.PostAsync("/api/v1/profiles/registration", new { orchardOwner = true, orchards = new[] { TestData.Orchard("Attacker's orchard") } });

        Assert.Equal(HttpStatusCode.NotFound, (await attacker.GetAsync($"/api/v1/orchards/{orchardId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.PutAsync($"/api/v1/orchards/{orchardId}", TestData.Orchard("Stolen"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.DeleteAsync($"/api/v1/orchards/{orchardId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/orchards/{orchardId}")).StatusCode);
        Assert.Equal("Owner's orchard", (await (await owner.GetAsync($"/api/v1/orchards/{orchardId}")).Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task User_cannot_see_another_users_profile()
    {
        var owner = await SignedInAsync();
        await owner.PostAsync("/api/v1/profiles/registration", new { picker = true, personalDetails = TestData.PersonalDetails() });
        var other = await SignedInAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/v1/pickers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/v1/personal-details/me")).StatusCode);
        var summary = await (await other.GetAsync("/api/v1/profiles")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("personalDetails").ValueKind);
    }

    [Theory]
    [InlineData("GET", "/api/v1/profiles")]
    [InlineData("GET", "/api/v1/personal-details/me")]
    [InlineData("GET", "/api/v1/pickers/me")]
    [InlineData("GET", "/api/v1/drivers/me")]
    [InlineData("GET", "/api/v1/vehicles")]
    [InlineData("GET", "/api/v1/orchards")]
    [InlineData("POST", "/api/v1/profiles/registration")]
    [InlineData("POST", "/api/v1/vehicles")]
    [InlineData("DELETE", "/api/v1/pickers/me")]
    public async Task Unauthenticated_requests_cannot_access_profile_endpoints(string method, string path)
    {
        var anonymous = new SessionClient(factory.CreateClient());

        var response = method == "GET"
            ? await anonymous.GetAsync(path)
            : await anonymous.SendAsync(new HttpMethod(method), path, method == "DELETE" ? null : new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
