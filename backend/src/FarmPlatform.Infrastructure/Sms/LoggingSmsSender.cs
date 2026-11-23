using FarmPlatform.Application.Common.Sms;
using Microsoft.Extensions.Logging;

namespace FarmPlatform.Infrastructure.Sms;

/// <summary>Development SMS sender: prints the message (including the consent code and link) to the console.</summary>
public sealed partial class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phoneNumber, string text, CancellationToken cancellationToken)
    {
        LogSms(logger, phoneNumber, text);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "\n===== SMS (not sent - development) =====\nTo: {Phone}\n{Text}\n========================================")]
    private static partial void LogSms(ILogger logger, string phone, string text);
}
