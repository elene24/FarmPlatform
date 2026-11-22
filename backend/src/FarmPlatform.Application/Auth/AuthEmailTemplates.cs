using System.Net;
using FarmPlatform.Application.Common.Email;

namespace FarmPlatform.Application.Auth;

/// <summary>Bilingual (Georgian / English) account emails. Language follows the user's preference.</summary>
public static class AuthEmailTemplates
{
    public static EmailMessage VerifyEmail(string to, string language, Uri link) => language == "en"
        ? Build(to, "Confirm your email — FarmPlatform",
            "Welcome to FarmPlatform!",
            "Please confirm your email address by opening this link:",
            "Confirm email",
            "The link is valid for 24 hours. If you did not create an account, you can ignore this email.",
            link)
        : Build(to, "დაადასტურეთ ელფოსტა — FarmPlatform",
            "მოგესალმებით FarmPlatform-ზე!",
            "გთხოვთ, დაადასტუროთ თქვენი ელფოსტა ამ ბმულზე გადასვლით:",
            "ელფოსტის დადასტურება",
            "ბმული მოქმედებს 24 საათის განმავლობაში. თუ ანგარიში თქვენ არ შეგიქმნიათ, უბრალოდ უგულებელყავით ეს წერილი.",
            link);

    public static EmailMessage ResetPassword(string to, string language, Uri link) => language == "en"
        ? Build(to, "Reset your password — FarmPlatform",
            "Password reset",
            "We received a request to reset your password. Open this link to choose a new one:",
            "Choose a new password",
            "The link is valid for 24 hours. If you did not ask for this, ignore this email — your password stays the same.",
            link)
        : Build(to, "პაროლის აღდგენა — FarmPlatform",
            "პაროლის აღდგენა",
            "მივიღეთ მოთხოვნა თქვენი პაროლის შესაცვლელად. ახალი პაროლის დასაყენებლად გადადით ბმულზე:",
            "ახალი პაროლის არჩევა",
            "ბმული მოქმედებს 24 საათის განმავლობაში. თუ ეს თქვენ არ მოგითხოვიათ, უგულებელყავით წერილი — პაროლი არ შეიცვლება.",
            link);

    private static EmailMessage Build(string to, string subject, string heading, string intro, string button, string footer, Uri link)
    {
        var url = link.AbsoluteUri;
        var text = $"{heading}\n\n{intro}\n{url}\n\n{footer}\n";

        var safeUrl = WebUtility.HtmlEncode(url);
        var html = $"""
            <!doctype html>
            <html><body style="margin:0;padding:24px;background:#f5f5f4;font-family:Arial,'Noto Sans Georgian',sans-serif;color:#1c1917">
              <table role="presentation" width="100%" style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;padding:32px">
                <tr><td>
                  <h1 style="margin:0 0 16px;font-size:20px;color:#24522d">{WebUtility.HtmlEncode(heading)}</h1>
                  <p style="margin:0 0 24px;font-size:15px;line-height:1.5">{WebUtility.HtmlEncode(intro)}</p>
                  <p style="margin:0 0 24px"><a href="{safeUrl}" style="display:inline-block;background:#2f6b3a;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:10px;font-size:15px">{WebUtility.HtmlEncode(button)}</a></p>
                  <p style="margin:0 0 8px;font-size:12px;color:#78716c;word-break:break-all">{safeUrl}</p>
                  <p style="margin:16px 0 0;font-size:12px;color:#78716c">{WebUtility.HtmlEncode(footer)}</p>
                </td></tr>
              </table>
            </body></html>
            """;

        return new EmailMessage(to, subject, text, html);
    }
}
