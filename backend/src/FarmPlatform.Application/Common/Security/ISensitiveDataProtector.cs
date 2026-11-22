using FarmPlatform.Domain.Profiles;

namespace FarmPlatform.Application.Common.Security;

/// <summary>
/// Application-level encryption for sensitive personal data (personal ID numbers, bank accounts).
/// Implemented in Infrastructure with AES-256-GCM plus an HMAC-SHA256 blind index.
/// </summary>
public interface ISensitiveDataProtector
{
    /// <summary>Normalizes, encrypts and (optionally) computes a blind index for <paramref name="plainValue"/>.</summary>
    ProtectedValue Protect(string plainValue, SensitiveDataKind kind, bool withBlindIndex);

    /// <summary>Decrypts a ciphertext produced by <see cref="Protect"/>. Throws if it was tampered with.</summary>
    string Unprotect(string ciphertext);

    /// <summary>
    /// Computes the blind index for a value so it can be looked up (e.g. "is this personal ID already registered?")
    /// without decrypting stored rows.
    /// </summary>
    string ComputeBlindIndex(string plainValue, SensitiveDataKind kind);
}

public enum SensitiveDataKind
{
    PersonalIdNumber = 1,
    BankAccountNumber = 2,
}
