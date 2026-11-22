using System.Numerics;

namespace FarmPlatform.Application.Profiles;

/// <summary>Format checks for identifiers used in Georgia.</summary>
public static class GeorgianIdentifiers
{
    /// <summary>Digits only, so "010 0101 1234" and "01001011234" compare equal.</summary>
    public static string DigitsOnly(string? value) => new((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    /// <summary>Georgian personal number (პირადი ნომერი): exactly 11 digits.</summary>
    public static bool IsPersonalNumber(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.All(c => char.IsAsciiDigit(c) || c == ' ') && DigitsOnly(trimmed).Length == 11;
    }

    /// <summary>Company identification code (საიდენტიფიკაციო კოდი) of an LLC and other legal entities: 9 digits.</summary>
    public static bool IsCompanyId(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.All(c => char.IsAsciiDigit(c) || c == ' ') && DigitsOnly(trimmed).Length == 9;
    }

    public static string NormalizeIban(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>
    /// ISO 13616 IBAN check (mod 97). Georgian IBANs must be 22 characters: GE + 2 check digits + 2-letter bank code
    /// + 16 digits (e.g. GE29NB0000000101904917). Other countries' IBANs are accepted if their checksum is valid.
    /// </summary>
    public static bool IsValidIban(string? value)
    {
        var iban = NormalizeIban(value);
        if (iban.Length is < 15 or > 34 || !char.IsAsciiLetterUpper(iban[0]) || !char.IsAsciiLetterUpper(iban[1])
            || !char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3]) || !iban.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        if (iban.StartsWith("GE", StringComparison.Ordinal)
            && (iban.Length != 22 || !char.IsAsciiLetterUpper(iban[4]) || !char.IsAsciiLetterUpper(iban[5]) || !iban[6..].All(char.IsAsciiDigit)))
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var numeric = string.Concat(rearranged.Select(c => char.IsAsciiDigit(c) ? c.ToString() : (c - 'A' + 10).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return BigInteger.Parse(numeric, System.Globalization.CultureInfo.InvariantCulture) % 97 == 1;
    }
}
