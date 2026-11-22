namespace FarmPlatform.Infrastructure.Security;

/// <summary>
/// Keys for application-level encryption of sensitive personal data. Supply via environment
/// variables / user-secrets only - never commit them:
///   Encryption__ActiveKeyId=k1
///   Encryption__Keys__k1=&lt;base64 32 bytes&gt;
///   Encryption__BlindIndexKey=&lt;base64 32 bytes&gt;
/// To rotate: add k2, set ActiveKeyId=k2, keep k1 until old rows are re-encrypted.
/// The blind-index key cannot be rotated without recomputing every hash - treat it as permanent.
/// </summary>
public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    public string ActiveKeyId { get; set; } = string.Empty;

    public Dictionary<string, string> Keys { get; set; } = [];

    public string BlindIndexKey { get; set; } = string.Empty;
}
