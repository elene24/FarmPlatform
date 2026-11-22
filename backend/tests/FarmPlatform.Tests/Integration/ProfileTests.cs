using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FarmPlatform.Tests.Integration;

public sealed class ProfileTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<SessionClient> SignedInAsync()
    {
        var session = new SessionClient(factory.CreateClient());
        (await session.RegisterAsync(TestData.NewEmail())).EnsureSuccessStatusCode();
        return session;
    }

    private static Task<HttpResponseMessage> Register(SessionClient s, bool picker = false, bool driver = false, bool orchard = false,
        object? personal = null, object[]? vehicles = null, object[]? orchards = null) =>
        s.PostAsync("/api/v1/profiles/registration", new { picker, driver, orchardOwner = orchard, personalDetails = personal, vehicles, orchards });

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static async Task<JsonElement> Summary(SessionClient s) => await Json(await s.GetAsync("/api/v1/profiles"));

    [Fact]
    public async Task Register_as_picker()
    {
        var s = await SignedInAsync();

        var response = await Register(s, picker: true, personal: TestData.PersonalDetails());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await Json(response);
        Assert.True(summary.GetProperty("picker").GetProperty("status").GetProperty("active").GetBoolean());
        Assert.False(summary.GetProperty("driver").GetProperty("status").GetProperty("registered").GetBoolean());
        Assert.Equal("ნინო", summary.GetProperty("personalDetails").GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Register_as_driver_with_vehicles()
    {
        var s = await SignedInAsync();

        var response = await Register(s, driver: true, personal: TestData.PersonalDetails(),
            vehicles: [TestData.Vehicle("AA-123-BB", 14), TestData.Vehicle("BB-456-CC", 18)]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vehicles = (await Json(response)).GetProperty("driver").GetProperty("vehicles");
        Assert.Equal(2, vehicles.GetArrayLength());
        Assert.Equal("AA123BB", vehicles[0].GetProperty("licensePlateNumber").GetString());
    }

    [Fact]
    public async Task Register_as_orchard_owner_with_several_orchards()
    {
        var s = await SignedInAsync();

        var response = await Register(s, orchard: true, orchards: [TestData.Orchard("North"), TestData.Orchard("South", statusId: 1, idNumber: "01001011234")]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orchards = (await Json(response)).GetProperty("orchardOwner").GetProperty("orchards");
        Assert.Equal(2, orchards.GetArrayLength());
        Assert.Equal("llc", orchards[0].GetProperty("legalEntityStatusCode").GetString());
        Assert.Equal(12.5m, orchards[0].GetProperty("areaHectares").GetDecimal());
    }

    [Fact]
    public async Task Picker_and_driver_on_one_account_share_one_set_of_personal_details()
    {
        var s = await SignedInAsync();

        var response = await Register(s, picker: true, driver: true, personal: TestData.PersonalDetails(), vehicles: [TestData.Vehicle()]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await Json(response);
        Assert.True(summary.GetProperty("picker").GetProperty("status").GetProperty("active").GetBoolean());
        Assert.True(summary.GetProperty("driver").GetProperty("status").GetProperty("active").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await s.GetAsync("/api/v1/pickers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.GetAsync("/api/v1/drivers/me")).StatusCode);
    }

    [Fact]
    public async Task Another_category_can_be_added_later_without_reentering_personal_details()
    {
        var s = await SignedInAsync();
        await Register(s, picker: true, personal: TestData.PersonalDetails());

        var response = await s.PostAsync("/api/v1/profiles/driver", new { vehicles = new[] { TestData.Vehicle() } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await Json(response);
        Assert.True(summary.GetProperty("picker").GetProperty("status").GetProperty("active").GetBoolean());
        Assert.True(summary.GetProperty("driver").GetProperty("status").GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task Registration_is_all_or_nothing()
    {
        var s = await SignedInAsync();

        // Valid personal details, but the vehicle is invalid: nothing may be stored.
        var response = await Register(s, picker: true, driver: true, personal: TestData.PersonalDetails(), vehicles: [TestData.Vehicle(capacity: 0)]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var summary = await Summary(s);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("personalDetails").ValueKind);
        Assert.False(summary.GetProperty("picker").GetProperty("status").GetProperty("registered").GetBoolean());
    }

    [Fact]
    public async Task A_personal_id_number_can_belong_to_only_one_account()
    {
        var pin = TestData.NewPersonalId();
        await Register(await SignedInAsync(), picker: true, personal: TestData.PersonalDetails(pin));

        var response = await Register(await SignedInAsync(), picker: true, personal: TestData.PersonalDetails(pin));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("profile.personal_id_taken", (await Json(response)).GetProperty("errors").GetProperty("personalDetails.personalIdNumber").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Personal_id_and_bank_account_are_never_returned_in_full()
    {
        var s = await SignedInAsync();
        var pin = TestData.NewPersonalId();
        var iban = TestData.NewIban();

        var registered = await (await Register(s, picker: true, personal: TestData.PersonalDetails(pin, iban))).Content.ReadAsStringAsync(Ct);
        var details = await (await s.GetAsync("/api/v1/personal-details/me")).Content.ReadAsStringAsync(Ct);

        foreach (var body in new[] { registered, details })
        {
            Assert.DoesNotContain(pin, body, StringComparison.Ordinal);
            Assert.DoesNotContain(iban, body, StringComparison.Ordinal);
            Assert.Contains($"\"personalIdNumberLast4\":\"{pin[^4..]}\"", body, StringComparison.Ordinal);
            Assert.Contains($"\"bankAccountLast4\":\"{iban[^4..]}\"", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Updating_personal_details_keeps_id_and_iban_when_left_empty()
    {
        var s = await SignedInAsync();
        var pin = TestData.NewPersonalId();
        await Register(s, picker: true, personal: TestData.PersonalDetails(pin));

        var response = await s.PutAsync("/api/v1/personal-details/me", new
        {
            firstName = "Nino",
            lastName = "Beridze",
            personalIdNumber = (string?)null,
            bankAccountNumber = "",
            legalAddress = "Tbilisi",
            actualAddress = "Gori",
            latitude = 41.7m,
            longitude = 44.8m,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("Nino", body.GetProperty("firstName").GetString());
        Assert.Equal(pin[^4..], body.GetProperty("personalIdNumberLast4").GetString());
    }

    [Fact]
    public async Task Deactivating_a_category_keeps_the_account_and_its_data()
    {
        var s = await SignedInAsync();
        await Register(s, driver: true, personal: TestData.PersonalDetails(), vehicles: [TestData.Vehicle()]);

        var response = await s.DeleteAsync("/api/v1/drivers/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.GetAsync("/api/v1/auth/me")).StatusCode);
        var driver = (await Summary(s)).GetProperty("driver");
        Assert.True(driver.GetProperty("status").GetProperty("registered").GetBoolean());
        Assert.False(driver.GetProperty("status").GetProperty("active").GetBoolean());
        Assert.Equal(1, driver.GetProperty("vehicles").GetArrayLength());

        // Re-registering reactivates it without new data.
        Assert.Equal(HttpStatusCode.OK, (await s.PostAsync("/api/v1/profiles/driver", new { })).StatusCode);
        Assert.True((await Summary(s)).GetProperty("driver").GetProperty("status").GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task Vehicles_can_be_added_edited_and_removed()
    {
        var s = await SignedInAsync();
        await Register(s, driver: true, personal: TestData.PersonalDetails(), vehicles: [TestData.Vehicle("AA-111-AA")]);

        var added = await Json(await s.PostAsync("/api/v1/vehicles", TestData.Vehicle("BB-222-BB", 30)));
        var id = added.GetProperty("id").GetString();
        var updated = await s.PutAsync($"/api/v1/vehicles/{id}", TestData.Vehicle("BB-222-BB", 40));
        var duplicate = await s.PostAsync("/api/v1/vehicles", TestData.Vehicle("aa 111 aa", 4));
        var deleted = await s.DeleteAsync($"/api/v1/vehicles/{id}");

        Assert.Equal(40, (await Json(updated)).GetProperty("passengerCapacity").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(1, (await Json(await s.GetAsync("/api/v1/vehicles"))).GetArrayLength());
    }

    [Fact]
    public async Task Orchards_can_be_added_edited_and_removed()
    {
        var s = await SignedInAsync();
        await Register(s, orchard: true, orchards: [TestData.Orchard("First")]);

        var added = await Json(await s.PostAsync("/api/v1/orchards", TestData.Orchard("Second", area: 3.25m)));
        var id = added.GetProperty("id").GetString();
        var updated = await s.PutAsync($"/api/v1/orchards/{id}", TestData.Orchard("Second (renamed)", area: 4m));
        var fetched = await Json(await s.GetAsync($"/api/v1/orchards/{id}"));
        var deleted = await s.DeleteAsync($"/api/v1/orchards/{id}");

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Second (renamed)", fetched.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.GetAsync($"/api/v1/orchards/{id}")).StatusCode);
    }

    // ---------------------------------------------------------------- validation

    [Fact]
    public async Task Invalid_personal_details_are_reported_per_field()
    {
        var s = await SignedInAsync();

        var response = await Register(s, picker: true, personal: new
        {
            firstName = "N1no",
            lastName = "",
            personalIdNumber = "1234567890",
            bankAccountNumber = "GE00TB0000000000000000",
            legalAddress = "x",
            actualAddress = "",
            latitude = 95m,
            longitude = 44m,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        Assert.Equal("validation.name_invalid", errors.GetProperty("personalDetails.firstName")[0].GetString());
        Assert.Equal("validation.required", errors.GetProperty("personalDetails.lastName")[0].GetString());
        Assert.Equal("validation.personal_id_invalid", errors.GetProperty("personalDetails.personalIdNumber")[0].GetString());
        Assert.Equal("validation.iban_invalid", errors.GetProperty("personalDetails.bankAccountNumber")[0].GetString());
        Assert.Equal("validation.required", errors.GetProperty("personalDetails.actualAddress")[0].GetString());
        Assert.Equal("validation.location_invalid", errors.GetProperty("personalDetails.location")[0].GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    [InlineData(-3)]
    public async Task Invalid_passenger_capacity_is_rejected(int capacity)
    {
        var s = await SignedInAsync();

        var response = await Register(s, driver: true, personal: TestData.PersonalDetails(), vehicles: [TestData.Vehicle(capacity: capacity)]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation.capacity_invalid", (await Json(response)).GetProperty("errors").GetProperty("vehicles.0.passengerCapacity")[0].GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100001)]
    [InlineData(1.23456)]
    public async Task Invalid_orchard_area_is_rejected(double area)
    {
        var s = await SignedInAsync();

        var response = await Register(s, orchard: true, orchards: [TestData.Orchard(area: (decimal)area)]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation.area_invalid", (await Json(response)).GetProperty("errors").GetProperty("orchards.0.areaHectares")[0].GetString());
    }

    [Theory]
    [InlineData(2, "01001011234", "validation.company_id_invalid")]  // LLC needs a 9-digit company code
    [InlineData(1, "404123456", "validation.personal_id_invalid")]   // entrepreneur needs an 11-digit personal number
    [InlineData(99, "404123456", "validation.legal_status_invalid")] // unknown legal status
    public async Task Orchard_identification_number_must_match_the_legal_status(int statusId, string idNumber, string expectedCode)
    {
        var s = await SignedInAsync();

        var response = await Register(s, orchard: true, orchards: [TestData.Orchard(statusId: statusId, idNumber: idNumber)]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        var codes = errors.EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString())).ToList();
        Assert.Contains(expectedCode, codes);
    }

    [Fact]
    public async Task Missing_required_data_is_reported()
    {
        var s = await SignedInAsync();

        var none = await Register(s);
        var driverWithoutVehicle = await Register(s, driver: true, personal: TestData.PersonalDetails());
        var pickerWithoutDetails = await Register(s, picker: true);

        Assert.Equal("profile.no_category_selected", (await Json(none)).GetProperty("code").GetString());
        Assert.Equal("profile.vehicle_required", (await Json(driverWithoutVehicle)).GetProperty("errors").GetProperty("vehicles")[0].GetString());
        Assert.Equal("profile.personal_details_required", (await Json(pickerWithoutDetails)).GetProperty("errors").GetProperty("personalDetails")[0].GetString());
    }
}
