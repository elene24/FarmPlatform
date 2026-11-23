using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Buffers.Text;

namespace FarmPlatform.Application.Linked;

/// <summary>Random consent tokens/codes. Only SHA-256 hashes are stored.</summary>
public static class ConsentSecrets
{
    /// <summary>32 random bytes, URL-safe. Goes into the consent link.</summary>
    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>6 random digits, easy to read out over the phone.</summary>
    public static string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public static string HashToken(string token) => Hash("token:" + token);

    /// <summary>Codes are hashed together with the record id so equal codes of different people differ.</summary>
    public static string HashCode(Guid linkedPersonId, string code) => Hash($"code:{linkedPersonId:N}:{code.Trim()}");

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
