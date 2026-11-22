using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using FarmPlatform.Application.Common.Email;
using Microsoft.Extensions.Options;

namespace FarmPlatform.Infrastructure.Email;

/// <summary>
/// Sends email through any SMTP relay (Brevo, Postmark, Amazon SES, Mailgun, ...). Credentials come from
/// environment variables (Email__Smtp__Username / Email__Smtp__Password), never from source control.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName, Encoding.UTF8),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.TextBody,
            BodyEncoding = Encoding.UTF8,
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(settings.Smtp.Host, settings.Smtp.Port)
        {
            EnableSsl = settings.Smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(settings.Smtp.Username, settings.Smtp.Password),
        };

        await client.SendMailAsync(mail, cancellationToken);
    }
}
