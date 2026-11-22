using FarmPlatform.Application.Common.Email;
using Microsoft.Extensions.Logging;

namespace FarmPlatform.Infrastructure.Email;

/// <summary>
/// Development email sender: prints the email (including its links) to the API console instead of sending it.
/// Never use in production - the log would contain account tokens.
/// </summary>
public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogEmail(logger, message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "\n===== EMAIL (not sent - development) =====\nTo: {To}\nSubject: {Subject}\n\n{Body}==========================================")]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);
}
