using System.Security.Cryptography;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace FarmPlatform.Tests.Unit;

public sealed class SensitiveDataProtectorTests
{
    private static AesGcmSensitiveDataProtector CreateProtector(string activeKeyId = "k1", Dictionary<string, string>? keys = null) =>
        new(Options.Create(new EncryptionOptions
        {
            ActiveKeyId = activeKeyId,
            Keys = keys ?? new Dictionary<string, string> { ["k1"] = TestKeys.DataKey },
            BlindIndexKey = TestKeys.BlindIndexKey,
        }));

    [Fact]
    public void Protect_then_Unprotect_round_trips_the_normalized_value()
    {
        var protector = CreateProtector();

        var protectedValue = protector.Protect("GE29 nb00 0000 0101 9049 17", SensitiveDataKind.BankAccountNumber, withBlindIndex: false);

        Assert.Equal("GE29NB0000000101904917", protector.Unprotect(protectedValue.Ciphertext));
        Assert.Equal("4917", protectedValue.Last4);
        Assert.DoesNotContain("904917", protectedValue.Ciphertext, StringComparison.Ordinal);
        Assert.StartsWith("k1:", protectedValue.Ciphertext, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_value_encrypts_differently_each_time_but_has_the_same_blind_index()
    {
        var protector = CreateProtector();

        var a = protector.Protect("01001011234", SensitiveDataKind.PersonalIdNumber, withBlindIndex: true);
        var b = protector.Protect("010 0101 1234", SensitiveDataKind.PersonalIdNumber, withBlindIndex: true);

        Assert.NotEqual(a.Ciphertext, b.Ciphertext);
        Assert.Equal(a.BlindIndex, b.BlindIndex);
        Assert.Equal(64, a.BlindIndex.Length);
        Assert.Equal(a.BlindIndex, protector.ComputeBlindIndex("01001011234", SensitiveDataKind.PersonalIdNumber));
    }

    [Fact]
    public void Blind_index_differs_by_kind()
    {
        var protector = CreateProtector();

        Assert.NotEqual(
            protector.ComputeBlindIndex("12345678901", SensitiveDataKind.PersonalIdNumber),
            protector.ComputeBlindIndex("12345678901", SensitiveDataKind.BankAccountNumber));
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        var protector = CreateProtector();
        var ciphertext = protector.Protect("01001011234", SensitiveDataKind.PersonalIdNumber, withBlindIndex: false).Ciphertext;

        var payload = Convert.FromBase64String(ciphertext[3..]);
        payload[^1] ^= 0x01;
        var tampered = "k1:" + Convert.ToBase64String(payload);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(tampered));
    }

    [Fact]
    public void Old_key_still_decrypts_after_rotation()
    {
        var keys = new Dictionary<string, string> { ["k1"] = TestKeys.DataKey };
        var oldCiphertext = CreateProtector("k1", keys).Protect("01001011234", SensitiveDataKind.PersonalIdNumber, false).Ciphertext;

        keys["k2"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var rotated = CreateProtector("k2", keys);

        Assert.Equal("01001011234", rotated.Unprotect(oldCiphertext));
        Assert.StartsWith("k2:", rotated.Protect("1", SensitiveDataKind.PersonalIdNumber, false).Ciphertext, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_keys_of_the_wrong_length()
    {
        var keys = new Dictionary<string, string> { ["k1"] = Convert.ToBase64String(new byte[16]) };

        Assert.Throws<InvalidOperationException>(() => CreateProtector("k1", keys));
    }
}
