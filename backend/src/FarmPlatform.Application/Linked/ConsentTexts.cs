using System.Globalization;

namespace FarmPlatform.Application.Linked;

/// <summary>
/// The consent request a linked person receives. The version is stored with every consent so it is always clear
/// what exactly the person agreed to; bump it whenever the wording or scope changes.
/// </summary>
public static class ConsentTexts
{
    public const string Version = "2026-11-23";

    /// <summary>Georgian SMS (the linked persons are Georgian-speaking pickers without an account).</summary>
    public static string ConsentRequestSms(string representativeName, string personFirstName, bool payToRepresentative, string? bankAccountLast4, string code, Uri link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var payment = payToRepresentative
            ? " და თქვენი ანაზღაურება ჩაირიცხოს მის ანგარიშზე"
            : bankAccountLast4 is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $" (ანაზღაურება: ანგარიში ****{bankAccountLast4})");

        return $"FarmPlatform: {personFirstName}, {representativeName} ითხოვს თანხმობას, რომ თქვენს ნაცვლად უპასუხოს კრეფის სამუშაოებზე{payment}. "
            + $"თანხმობის კოდი: {code}. დეტალები, თანხმობა ან უარი: {link.AbsoluteUri}";
    }

    public static string RepresentationEndedSms(string representativeName) =>
        $"FarmPlatform: {representativeName} აღარ არის თქვენი წარმომადგენელი. თქვენი მონაცემები წაიშალა.";
}
