using FarmPlatform.Application.Profiles;

namespace FarmPlatform.Tests.Unit;

public sealed class ProfileValidationTests
{
    [Theory]
    [InlineData("01001011234", true)]
    [InlineData("010 0101 1234", true)]
    [InlineData("0100101123", false)]   // 10 digits
    [InlineData("010010112345", false)] // 12 digits
    [InlineData("0100101123a", false)]
    [InlineData("", false)]
    public void Personal_number_has_11_digits(string value, bool valid) =>
        Assert.Equal(valid, GeorgianIdentifiers.IsPersonalNumber(value));

    [Theory]
    [InlineData("GE29NB0000000101904917", true)]      // official example
    [InlineData("ge29 nb00 0000 0101 9049 17", true)] // spaces / lower case
    [InlineData("GE28NB0000000101904917", false)]     // wrong check digits
    [InlineData("GE29NB000000010190491", false)]      // too short for Georgia
    [InlineData("GE2912000000000101904917", false)]   // bank code must be letters
    [InlineData("DE89370400440532013000", true)]      // valid foreign IBAN
    [InlineData("1234", false)]
    public void Iban_checksum_and_georgian_format(string value, bool valid) =>
        Assert.Equal(valid, GeorgianIdentifiers.IsValidIban(value));

    [Theory]
    [InlineData("llc", "404123456", true)]
    [InlineData("llc", "01001011234", false)]
    [InlineData("individual_entrepreneur", "01001011234", true)]
    [InlineData("natural_person", "404123456", false)]
    [InlineData("other", "404123456", true)]
    [InlineData("other", "01001011234", true)]
    public void Identification_number_depends_on_legal_status(string status, string value, bool valid) =>
        Assert.Equal(valid, ProfileValidator.IsValidIdentificationNumber(value, status));
}
