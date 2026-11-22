using System.Security.Cryptography;
using System.Text;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Domain.Profiles;
using Microsoft.Extensions.Options;

namespace FarmPlatform.Infrastructure.Security;

/// <summary>
/// AES-256-GCM authenticated encryption with a random 96-bit nonce per value.
/// Stored format: "{keyId}:{base64(nonce | ciphertext | tag)}". The key id is bound as associated
/// data, so a ciphertext cannot be silently moved to a different key.
/// Blind index: HMAC-SHA256(blindIndexKey, kind + ":" + normalizedValue), hex-encoded.
/// </summary>
public sealed class AesGcmSensitiveDataProtector : ISensitiveDataProtector
{
    private const int KeySize = 32;
    private const int NonceSize = 12; // AesGcm.NonceByteSizes.MaxSize
    private const int TagSize = 16;   // AesGcm.TagByteSizes.MaxSize

    private readonly string _activeKeyId;
    private readonly Dictionary<string, byte[]> _keys;
    private readonly byte[] _blindIndexKey;

    public AesGcmSensitiveDataProtector(IOptions<EncryptionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;

        _keys = value.Keys.ToDictionary(k => k.Key, k => DecodeKey(k.Value, $"Encryption:Keys:{k.Key}"), StringComparer.Ordinal);
        if (!_keys.ContainsKey(value.ActiveKeyId))
        {
            throw new InvalidOperationException("Encryption:ActiveKeyId must name one of the configured Encryption:Keys.");
        }

        _activeKeyId = value.ActiveKeyId;
        _blindIndexKey = DecodeKey(value.BlindIndexKey, "Encryption:BlindIndexKey");
    }

    public ProtectedValue Protect(string plainValue, SensitiveDataKind kind, bool withBlindIndex)
    {
        var normalized = SensitiveDataNormalizer.Normalize(plainValue, kind);
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Value is empty after normalization.", nameof(plainValue));
        }

        var ciphertext = Encrypt(normalized);
        var blindIndex = withBlindIndex ? HashNormalized(normalized, kind) : string.Empty;
        var last4 = normalized.Length <= 4 ? normalized : normalized[^4..];
        return new ProtectedValue(ciphertext, blindIndex, last4);
    }

    public string Unprotect(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);

        var separator = ciphertext.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
        {
            throw new CryptographicException("Malformed protected value.");
        }

        var keyId = ciphertext[..separator];
        if (!_keys.TryGetValue(keyId, out var key))
        {
            throw new CryptographicException($"Unknown encryption key id '{keyId}'.");
        }

        var payload = Convert.FromBase64String(ciphertext[(separator + 1)..]);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Malformed protected value.");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var cipher = payload.AsSpan(NonceSize, payload.Length - NonceSize - TagSize);
        var tag = payload.AsSpan(payload.Length - TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(keyId));
        return Encoding.UTF8.GetString(plain);
    }

    public string ComputeBlindIndex(string plainValue, SensitiveDataKind kind) =>
        HashNormalized(SensitiveDataNormalizer.Normalize(plainValue, kind), kind);

    private string Encrypt(string normalized)
    {
        var key = _keys[_activeKeyId];
        var plain = Encoding.UTF8.GetBytes(normalized);
        var payload = new byte[NonceSize + plain.Length + TagSize];
        var nonce = payload.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(
            nonce,
            plain,
            payload.AsSpan(NonceSize, plain.Length),
            payload.AsSpan(NonceSize + plain.Length, TagSize),
            Encoding.UTF8.GetBytes(_activeKeyId));

        return $"{_activeKeyId}:{Convert.ToBase64String(payload)}";
    }

    private string HashNormalized(string normalized, SensitiveDataKind kind)
    {
        var input = Encoding.UTF8.GetBytes($"{(int)kind}:{normalized}");
        return Convert.ToHexStringLower(HMACSHA256.HashData(_blindIndexKey, input));
    }

    private static byte[] DecodeKey(string base64, string name)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64 ?? string.Empty);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{name} must be base64.", ex);
        }

        if (bytes.Length != KeySize)
        {
            throw new InvalidOperationException($"{name} must be exactly {KeySize} bytes (base64 of 32 random bytes).");
        }

        return bytes;
    }
}
