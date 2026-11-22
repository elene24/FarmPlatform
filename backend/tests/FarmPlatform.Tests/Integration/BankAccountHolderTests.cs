using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FarmPlatform.Tests.Integration;

/// <summary>"This bank account does not belong to me" and the alternative phone number.</summary>
public sealed class BankAccountHolderTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<SessionClient> SignedInAsync()
    {
        var session = new SessionClient(factory.CreateClient());
        (await session.RegisterAsync(TestData.NewEmail())).EnsureSuccessStatusCode();
        return session;
    }

    private static Dictionary<string, object?> Personal(string? pin = null) => new()
    {
        ["firstName"] = "ნინო",
        ["lastName"] = "ბერიძე",
        ["personalIdNumber"] = pin ?? TestData.NewPersonalId(),
        ["bankAccountNumber"] = TestData.NewIban(),
        ["legalAddress"] = "თბილისი",
        ["actualAddress"] = "გორი",
        ["latitude"] = 41.98m,
        ["longitude"] = 44.11m,
    };

    private static Task<HttpResponseMessage> RegisterPicker(SessionClient s, Dictionary<string, object?> personal) =>
        s.PostAsync("/api/v1/profiles/registration", new { picker = true, personalDetails = personal });

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static string? FirstError(JsonElement body, string field) => body.GetProperty("errors").GetProperty(field)[0].GetString();

    [Fact]
    public async Task Someone_elses_account_requires_the_holders_name_and_personal_id()
    {
        var personal = Personal();
        personal["bankAccountOwnedByOther"] = true;

        var response = await RegisterPicker(await SignedInAsync(), personal);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("validation.required", FirstError(body, "personalDetails.accountHolderFirstName"));
        Assert.Equal("validation.required", FirstError(body, "personalDetails.accountHolderLastName"));
        Assert.Equal("validation.required", FirstError(body, "personalDetails.accountHolderPersonalIdNumber"));
    }

    [Fact]
    public async Task Holder_fields_are_ignored_when_the_account_is_the_users_own()
    {
        var personal = Personal();
        personal["accountHolderFirstName"] = "x";

        var response = await RegisterPicker(await SignedInAsync(), personal);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = (await Json(response)).GetProperty("personalDetails");
        Assert.False(details.GetProperty("bankAccountOwnedByOther").GetBoolean());
        Assert.Equal(JsonValueKind.Null, details.GetProperty("accountHolderFirstName").ValueKind);
    }

    [Fact]
    public async Task Holder_details_are_saved_and_the_holders_id_is_masked()
    {
        var s = await SignedInAsync();
        var holderPin = TestData.NewPersonalId();
        var personal = Personal();
        personal["bankAccountOwnedByOther"] = true;
        personal["accountHolderFirstName"] = "თამარ";
        personal["accountHolderLastName"] = "ბერიძე";
        personal["accountHolderPersonalIdNumber"] = holderPin;

        var response = await RegisterPicker(s, personal);
        var raw = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(holderPin, raw, StringComparison.Ordinal);
        var details = (await (await s.GetAsync("/api/v1/personal-details/me")).Content.ReadFromJsonAsync<JsonElement>(Ct));
        Assert.True(details.GetProperty("bankAccountOwnedByOther").GetBoolean());
        Assert.Equal("თამარ", details.GetProperty("accountHolderFirstName").GetString());
        Assert.Equal(holderPin[^4..], details.GetProperty("accountHolderPersonalIdLast4").GetString());
    }

    [Fact]
    public async Task The_holder_cannot_be_the_user_themselves()
    {
        var s = await SignedInAsync();
        var pin = TestData.NewPersonalId();
        var personal = Personal(pin);
        personal["bankAccountOwnedByOther"] = true;
        personal["accountHolderFirstName"] = "ნინო";
        personal["accountHolderLastName"] = "ბერიძე";
        personal["accountHolderPersonalIdNumber"] = pin;

        var inRegistration = await RegisterPicker(s, personal);
        Assert.Equal("validation.holder_is_self", FirstError(await Json(inRegistration), "personalDetails.accountHolderPersonalIdNumber"));

        // Also when the user's own ID is not re-sent (edit form keeps it): compared with the stored one.
        await RegisterPicker(s, Personal(pin));
        personal["personalIdNumber"] = null;
        var onUpdate = await s.PutAsync("/api/v1/personal-details/me", personal);
        Assert.Equal("validation.holder_is_self", FirstError(await Json(onUpdate), "accountHolderPersonalIdNumber"));
    }

    [Fact]
    public async Task Editing_keeps_the_holders_id_when_omitted_and_unticking_clears_holder_details()
    {
        var s = await SignedInAsync();
        var holderPin = TestData.NewPersonalId();
        var personal = Personal();
        personal["bankAccountOwnedByOther"] = true;
        personal["accountHolderFirstName"] = "თამარ";
        personal["accountHolderLastName"] = "ბერიძე";
        personal["accountHolderPersonalIdNumber"] = holderPin;
        await RegisterPicker(s, personal);

        personal["personalIdNumber"] = null;
        personal["bankAccountNumber"] = null;
        personal["accountHolderLastName"] = "მაისურაძე";
        personal["accountHolderPersonalIdNumber"] = "";
        var kept = await Json(await s.PutAsync("/api/v1/personal-details/me", personal));
        Assert.Equal("მაისურაძე", kept.GetProperty("accountHolderLastName").GetString());
        Assert.Equal(holderPin[^4..], kept.GetProperty("accountHolderPersonalIdLast4").GetString());

        personal["bankAccountOwnedByOther"] = false;
        var cleared = await Json(await s.PutAsync("/api/v1/personal-details/me", personal));
        Assert.False(cleared.GetProperty("bankAccountOwnedByOther").GetBoolean());
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("accountHolderPersonalIdLast4").ValueKind);
    }

    [Fact]
    public async Task Alternative_phone_is_optional_normalized_and_validated()
    {
        var s = await SignedInAsync();
        var personal = Personal();
        personal["alternativePhoneNumber"] = "599 11 22 33";

        var saved = await Json(await RegisterPicker(s, personal));
        Assert.Equal("+995599112233", saved.GetProperty("personalDetails").GetProperty("alternativePhoneNumber").GetString());

        personal["personalIdNumber"] = null;
        personal["alternativePhoneNumber"] = "12345";
        var invalid = await s.PutAsync("/api/v1/personal-details/me", personal);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("validation.phone_invalid", FirstError(await Json(invalid), "alternativePhoneNumber"));

        personal["alternativePhoneNumber"] = "";
        var removed = await Json(await s.PutAsync("/api/v1/personal-details/me", personal));
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("alternativePhoneNumber").ValueKind);
    }
}
