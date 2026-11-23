using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FarmPlatform.Application.Common.Sms;

namespace FarmPlatform.Tests.Integration;

/// <summary>Test double that records SMS messages so tests can read consent codes and links.</summary>
public sealed partial class CapturingSmsSender : ISmsSender
{
    private readonly ConcurrentQueue<(string Phone, string Text)> _messages = new();

    public IReadOnlyCollection<(string Phone, string Text)> Messages => _messages;

    public Task SendAsync(string phoneNumber, string text, CancellationToken cancellationToken)
    {
        _messages.Enqueue((phoneNumber, text));
        return Task.CompletedTask;
    }

    public string LastTo(string phone) => _messages.Last(m => m.Phone == phone).Text;

    /// <summary>The 6-digit code from the latest consent SMS to <paramref name="phone"/>.</summary>
    public string LastCode(string phone) => CodePattern().Match(LastTo(phone)).Groups[1].Value;

    /// <summary>The token from the latest consent link sent to <paramref name="phone"/>.</summary>
    public string LastToken(string phone) => TokenPattern().Match(LastTo(phone)).Groups[1].Value;

    [GeneratedRegex(@"კოდი: (\d{6})")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"/c/([A-Za-z0-9_\-]+)")]
    private static partial Regex TokenPattern();
}
