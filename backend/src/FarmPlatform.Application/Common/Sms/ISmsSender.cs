namespace FarmPlatform.Application.Common.Sms;

/// <summary>Sends SMS messages (consent requests to linked persons). Provider is configured in Infrastructure.</summary>
public interface ISmsSender
{
    /// <param name="phoneNumber">E.164, e.g. +995599123456.</param>
    Task SendAsync(string phoneNumber, string text, CancellationToken cancellationToken);
}
