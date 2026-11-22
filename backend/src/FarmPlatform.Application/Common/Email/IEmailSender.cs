namespace FarmPlatform.Application.Common.Email;

/// <summary>Sends transactional email (verification, password reset, later notifications).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>A ready-to-send email. Both bodies are provided so every mail client renders something sensible.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);
