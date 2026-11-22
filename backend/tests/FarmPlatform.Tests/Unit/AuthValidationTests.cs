using FarmPlatform.Application.Auth;

namespace FarmPlatform.Tests.Unit;

public sealed class AuthValidationTests
{
    [Theory]
    [InlineData("555123456", "+995555123456")]
    [InlineData("555 12 34 56", "+995555123456")]
    [InlineData("+995 555 12-34-56", "+995555123456")]
    [InlineData("00995555123456", "+995555123456")]
    [InlineData("995555123456", "+995555123456")]
    [InlineData("(555) 12.34.56", "+995555123456")]
    [InlineData("+44 7700 900123", "+447700900123")]
    public void Phone_numbers_are_normalized_to_E164(string input, string expected)
    {
        Assert.True(PhoneNumberNormalizer.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("322123456")]          // Georgian landline without +995: ambiguous, rejected
    [InlineData("+995 555 12 34")]     // too short for Georgia
    [InlineData("+995 555 12 34 567")] // too long for Georgia
    [InlineData("555-abc-123")]
    [InlineData("+0 123 456 789")]
    public void Invalid_phone_numbers_are_rejected(string input) =>
        Assert.False(PhoneNumberNormalizer.TryNormalize(input, out _));

    [Theory]
    [InlineData("nino@example.ge", true)]
    [InlineData(" nino@example.ge ", true)]
    [InlineData("nino@example", false)]
    [InlineData("Nino <nino@example.ge>", false)]
    [InlineData("nino@", false)]
    [InlineData("", false)]
    public void Email_format(string email, bool valid) =>
        Assert.Equal(valid, AuthValidator.IsValidEmail(email));

    [Fact]
    public void Registration_requires_terms_and_matching_passwords()
    {
        var errors = AuthValidator.Validate(new RegisterCommand("a@b.ge", "long-enough-pw", "other-password", "555123456", false, "ka"));

        Assert.Contains(errors, e => e.Code == "validation.passwords_mismatch" && e.Field == "confirmPassword");
        Assert.Contains(errors, e => e.Code == "validation.terms_required" && e.Field == "acceptTerms");
    }

    [Fact]
    public void Missing_fields_are_reported_as_required()
    {
        var errors = AuthValidator.Validate(new RegisterCommand(null, null, null, null, true, null));

        Assert.Contains(errors, e => e.Code == "validation.required" && e.Field == "email");
        Assert.Contains(errors, e => e.Code == "validation.required" && e.Field == "password");
        Assert.Contains(errors, e => e.Code == "validation.required" && e.Field == "phoneNumber");
    }
}
