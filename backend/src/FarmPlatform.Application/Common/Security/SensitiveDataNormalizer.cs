namespace FarmPlatform.Application.Common.Security;

/// <summary>
/// Canonical form of sensitive values before hashing/encryption, so "GE29 NB00 ..." and "ge29nb00..."
/// produce the same blind index.
/// </summary>
public static class SensitiveDataNormalizer
{
    public static string Normalize(string value, SensitiveDataKind kind)
    {
        ArgumentNullException.ThrowIfNull(value);

        return kind switch
        {
            // Personal ID numbers are digits only.
            SensitiveDataKind.PersonalIdNumber => new string(value.Where(char.IsDigit).ToArray()),
            // IBANs: strip spaces/dashes, upper-case.
            SensitiveDataKind.BankAccountNumber => new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sensitive data kind."),
        };
    }
}
