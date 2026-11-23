using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FarmPlatform.Tests.Integration;

/// <summary>Representatives, linked pickers without accounts, and SMS consent.</summary>
public sealed class LinkedPersonTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Base = "/api/v1/linked-persons";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewMobile() => "+9955" + string.Concat(Enumerable.Range(0, 8).Select(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(10)));

    /// <summary>A signed-in representative who has filled in their own personal details.</summary>
    private async Task<(SessionClient Session, string Pin)> RepresentativeAsync()
    {
        var session = new SessionClient(factory.CreateClient());
        (await session.RegisterAsync(TestData.NewEmail())).EnsureSuccessStatusCode();
        var pin = TestData.NewPersonalId();
        (await session.PutAsync("/api/v1/personal-details/me", TestData.PersonalDetails(pin))).EnsureSuccessStatusCode();
        return (session, pin);
    }

    private static Dictionary<string, object?> Person(string? phone = null, string? pin = null, bool payToRepresentative = false) => new()
    {
        ["firstName"] = "ვანო",
        ["lastName"] = "კაპანაძე",
        ["personalIdNumber"] = pin ?? TestData.NewPersonalId(),
        ["phoneNumber"] = phone ?? NewMobile(),
        ["legalAddress"] = "სოფ. ტყვიავი",
        ["actualAddress"] = "სოფ. ტყვიავი",
        ["latitude"] = 42.02m,
        ["longitude"] = 44.09m,
        ["payToRepresentative"] = payToRepresentative,
        ["bankAccountNumber"] = payToRepresentative ? null : TestData.NewIban(),
    };

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static async Task<string?> Code(HttpResponseMessage r) => (await Json(r)).GetProperty("code").GetString();

    private static async Task<(string Id, string Phone)> AddAsync(SessionClient s, Dictionary<string, object?>? person = null)
    {
        person ??= Person();
        var response = await s.PostAsync(Base, person);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await Json(response)).GetProperty("id").GetString()!, (string)person["phoneNumber"]!);
    }

    [Fact]
    public async Task Representative_must_identify_themselves_first()
    {
        var session = new SessionClient(factory.CreateClient());
        await session.RegisterAsync(TestData.NewEmail());

        var response = await session.PostAsync(Base, Person());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("linked.representative_details_required", await Code(response));
    }

    [Fact]
    public async Task Adding_a_person_sends_a_consent_sms_and_they_stay_pending()
    {
        var (s, _) = await RepresentativeAsync();
        var person = Person();
        var pin = (string)person["personalIdNumber"]!;

        var response = await s.PostAsync(Base, person);
        var raw = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain(pin, raw, StringComparison.Ordinal);
        var body = await Json(response);
        Assert.Equal("pending", body.GetProperty("consentStatus").GetString());
        Assert.Equal(pin[^4..], body.GetProperty("personalIdNumberLast4").GetString());

        var sms = factory.Sms.LastTo((string)person["phoneNumber"]!);
        Assert.Contains("ნინო ბერიძე", sms, StringComparison.Ordinal); // representative's name
        Assert.Matches(@"კოდი: \d{6}", sms);
        Assert.Contains("https://app.test/c/", sms, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Representative_confirms_with_the_code_from_the_persons_sms()
    {
        var (s, _) = await RepresentativeAsync();
        var (id, phone) = await AddAsync(s);

        var wrong = await s.PostAsync($"{Base}/{id}/confirm", new { code = "000000" == factory.Sms.LastCode(phone) ? "111111" : "000000" });
        var right = await s.PostAsync($"{Base}/{id}/confirm", new { code = factory.Sms.LastCode(phone) });

        Assert.Equal("linked.code_invalid", await Code(wrong));
        var body = await Json(right);
        Assert.Equal("confirmed", body.GetProperty("consentStatus").GetString());
        Assert.Equal("code", body.GetProperty("consentMethod").GetString());
    }

    [Fact]
    public async Task Code_stops_working_after_five_wrong_attempts()
    {
        var (s, _) = await RepresentativeAsync();
        var (id, phone) = await AddAsync(s);
        var correct = factory.Sms.LastCode(phone);
        var wrong = correct == "123456" ? "654321" : "123456";

        for (var i = 0; i < 5; i++)
        {
            await s.PostAsync($"{Base}/{id}/confirm", new { code = wrong });
        }

        var response = await s.PostAsync($"{Base}/{id}/confirm", new { code = correct });
        Assert.Equal("linked.code_expired", await Code(response));
    }

    [Fact]
    public async Task Person_can_agree_and_later_withdraw_through_the_link()
    {
        var (s, _) = await RepresentativeAsync();
        var (id, phone) = await AddAsync(s);
        var token = factory.Sms.LastToken(phone);
        var anonymous = new SessionClient(factory.CreateClient());

        var request = await Json(await anonymous.GetAsync($"/api/v1/consent/{token}"));
        Assert.Equal("ვანო", request.GetProperty("personFirstName").GetString());
        Assert.Equal("ნინო ბერიძე", request.GetProperty("representativeName").GetString());
        Assert.False(request.TryGetProperty("personalIdNumberLast4", out _));

        var accepted = await Json(await anonymous.PostAsync($"/api/v1/consent/{token}/accept"));
        Assert.Equal("confirmed", accepted.GetProperty("consentStatus").GetString());
        var seenByRepresentative = await Json(await s.GetAsync($"{Base}/{id}"));
        Assert.Equal("link", seenByRepresentative.GetProperty("consentMethod").GetString());

        var withdrawn = await Json(await anonymous.PostAsync($"/api/v1/consent/{token}/withdraw"));
        Assert.Equal("revoked", withdrawn.GetProperty("consentStatus").GetString());
        Assert.Equal("revoked", (await Json(await s.GetAsync($"{Base}/{id}"))).GetProperty("consentStatus").GetString());
    }

    [Fact]
    public async Task Person_can_decline()
    {
        var (s, _) = await RepresentativeAsync();
        var (id, phone) = await AddAsync(s);

        await new SessionClient(factory.CreateClient()).PostAsync($"/api/v1/consent/{factory.Sms.LastToken(phone)}/decline");

        Assert.Equal("declined", (await Json(await s.GetAsync($"{Base}/{id}"))).GetProperty("consentStatus").GetString());
    }

    [Fact]
    public async Task Paying_the_representative_is_stated_in_the_sms_and_needs_no_bank_account_of_the_person()
    {
        var (s, _) = await RepresentativeAsync();
        var person = Person(payToRepresentative: true);

        var response = await s.PostAsync(Base, person);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("payToRepresentative").GetBoolean());
        Assert.Contains("ანაზღაურება ჩაირიცხოს მის ანგარიშზე", factory.Sms.LastTo((string)person["phoneNumber"]!), StringComparison.Ordinal);
        var consent = await Json(await new SessionClient(factory.CreateClient()).GetAsync($"/api/v1/consent/{factory.Sms.LastToken((string)person["phoneNumber"]!)}"));
        Assert.True(consent.GetProperty("payToRepresentative").GetBoolean());
    }

    [Fact]
    public async Task Changing_the_payment_arrangement_asks_for_consent_again()
    {
        var (s, _) = await RepresentativeAsync();
        var person = Person();
        var (id, phone) = await AddAsync(s, person);
        await s.PostAsync($"{Base}/{id}/confirm", new { code = factory.Sms.LastCode(phone) });
        var smsBefore = factory.Sms.Messages.Count(m => m.Phone == phone);

        person["payToRepresentative"] = true;
        person["personalIdNumber"] = null;
        var updated = await Json(await s.PutAsync($"{Base}/{id}", person));

        Assert.Equal("pending", updated.GetProperty("consentStatus").GetString());
        Assert.Equal(smsBefore + 1, factory.Sms.Messages.Count(m => m.Phone == phone));
    }

    [Fact]
    public async Task Editing_the_name_keeps_consent()
    {
        var (s, _) = await RepresentativeAsync();
        var person = Person();
        var (id, phone) = await AddAsync(s, person);
        await s.PostAsync($"{Base}/{id}/confirm", new { code = factory.Sms.LastCode(phone) });

        person["lastName"] = "კაპანაძე-ბერიძე";
        person["personalIdNumber"] = null;
        person["bankAccountNumber"] = null;
        var updated = await Json(await s.PutAsync($"{Base}/{id}", person));

        Assert.Equal("confirmed", updated.GetProperty("consentStatus").GetString());
        Assert.Equal("კაპანაძე-ბერიძე", updated.GetProperty("lastName").GetString());
    }

    [Fact]
    public async Task One_person_one_profile_across_accounts_and_representatives()
    {
        var (repA, _) = await RepresentativeAsync();
        var (repB, repBPin) = await RepresentativeAsync();
        var pin = TestData.NewPersonalId();
        await AddAsync(repA, Person(pin: pin));

        var linkedTwice = await repB.PostAsync(Base, Person(pin: pin));
        var registeredUser = await repA.PostAsync(Base, Person(pin: repBPin));
        var self = new SessionClient(factory.CreateClient());
        await self.RegisterAsync(TestData.NewEmail());
        var ownProfileWithLinkedId = await self.PutAsync("/api/v1/personal-details/me", TestData.PersonalDetails(pin));

        Assert.Equal("profile.personal_id_taken", await Code(linkedTwice));
        Assert.Equal("profile.personal_id_taken", await Code(registeredUser));
        Assert.Equal(HttpStatusCode.Conflict, ownProfileWithLinkedId.StatusCode);
    }

    [Fact]
    public async Task Representative_cannot_link_themselves_or_use_their_own_phone()
    {
        var (s, pin) = await RepresentativeAsync();

        var self = await s.PostAsync(Base, Person(pin: pin));
        var ownPhone = await s.PostAsync(Base, Person(phone: "555 12 34 56"));
        var landline = await s.PostAsync(Base, Person(phone: "+995 32 212 34 56"));

        Assert.Equal("linked.cannot_link_self", await Code(self));
        Assert.Equal("linked.phone_is_representatives", await Code(ownPhone));
        Assert.Equal("validation.phone_not_mobile", (await Json(landline)).GetProperty("errors").GetProperty("phoneNumber")[0].GetString());
    }

    [Fact]
    public async Task Another_representative_cannot_see_or_change_the_person()
    {
        var (owner, _) = await RepresentativeAsync();
        var (id, phone) = await AddAsync(owner);
        var (other, _) = await RepresentativeAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Base}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsync($"{Base}/{id}", Person())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"{Base}/{id}/confirm", new { code = factory.Sms.LastCode(phone) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"{Base}/{id}/resend-consent")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"{Base}/{id}")).StatusCode);
        Assert.Equal(0, (await Json(await other.GetAsync(Base))).GetArrayLength());
        Assert.Equal("pending", (await Json(await owner.GetAsync($"{Base}/{id}"))).GetProperty("consentStatus").GetString());
    }

    [Fact]
    public async Task Removing_a_person_deletes_them_and_tells_them_by_sms()
    {
        var (s, _) = await RepresentativeAsync();
        var (id, phone) = await AddAsync(s);

        Assert.Equal(HttpStatusCode.NoContent, (await s.DeleteAsync($"{Base}/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await s.GetAsync($"{Base}/{id}")).StatusCode);
        Assert.Contains("აღარ არის თქვენი წარმომადგენელი", factory.Sms.LastTo(phone), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consent_sms_cannot_be_resent_immediately()
    {
        var (s, _) = await RepresentativeAsync();
        var (id, _) = await AddAsync(s);

        var response = await s.PostAsync($"{Base}/{id}/resend-consent");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("linked.sms_too_soon", await Code(response));
    }

    [Fact]
    public async Task Invalid_input_is_reported_per_field()
    {
        var (s, _) = await RepresentativeAsync();
        var person = Person();
        person["personalIdNumber"] = "123";
        person["phoneNumber"] = "";
        person["bankAccountNumber"] = "GE00XX";
        person["latitude"] = null;

        var errors = (await Json(await s.PostAsync(Base, person))).GetProperty("errors");

        Assert.Equal("validation.personal_id_invalid", errors.GetProperty("personalIdNumber")[0].GetString());
        Assert.Equal("validation.required", errors.GetProperty("phoneNumber")[0].GetString());
        Assert.Equal("validation.iban_invalid", errors.GetProperty("bankAccountNumber")[0].GetString());
        Assert.Equal("validation.location_required", errors.GetProperty("location")[0].GetString());
    }

    [Fact]
    public async Task Endpoints_require_sign_in_and_unknown_consent_links_are_not_found()
    {
        var anonymous = new SessionClient(factory.CreateClient());

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Base)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Base, Person())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/consent/not-a-real-token")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsync("/api/v1/consent/not-a-real-token/accept")).StatusCode);
    }
}
