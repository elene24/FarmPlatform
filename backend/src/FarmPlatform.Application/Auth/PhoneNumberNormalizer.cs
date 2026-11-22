using System.Text.RegularExpressions;

namespace FarmPlatform.Application.Auth;

/// <summary>
/// Normalizes phone numbers to E.164 ("+995555123456"). Accepts the ways people in Georgia usually type
/// numbers: "555 12 34 56", "+995 555 12-34-56", "00995555123456". Foreign numbers must start with + or 00.
/// </summary>
public static partial class PhoneNumberNormalizer
{
    private const string GeorgiaCountryCode = "995";

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());

        // Anything besides digits, one leading +, and common separators is rejected.
        if (!AllowedCharacters().IsMatch(trimmed))
        {
            return false;
        }

        if (!hasPlus && digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
            hasPlus = true;
        }

        if (!hasPlus)
        {
            if (digits.Length == 9 && digits[0] == '5')
            {
                digits = GeorgiaCountryCode + digits; // local Georgian mobile number
            }
            else if (digits.Length == 12 && digits.StartsWith(GeorgiaCountryCode, StringComparison.Ordinal))
            {
                // "995555123456" typed without the plus.
            }
            else
            {
                return false;
            }
        }

        if (digits.StartsWith(GeorgiaCountryCode, StringComparison.Ordinal) && digits.Length != 12)
        {
            return false; // Georgian numbers are +995 followed by exactly 9 digits.
        }

        if (digits.Length is < 8 or > 15 || digits[0] == '0')
        {
            return false;
        }

        normalized = "+" + digits;
        return true;
    }

    [GeneratedRegex(@"^\+?[0-9\s\-().]+$")]
    private static partial Regex AllowedCharacters();
}
