using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FarmPlatform.Application.Common.Email;
using Microsoft.AspNetCore.WebUtilities;

namespace FarmPlatform.Tests.Integration;

/// <summary>Test double that records emails so tests can follow verification / reset links.</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyCollection<EmailMessage> Messages => _messages;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Query parameters of the most recent link to <paramref name="path"/> sent to <paramref name="to"/>.</summary>
    public Dictionary<string, string> LastLinkQuery(string to, string path)
    {
        var message = _messages.Last(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase) && m.TextBody.Contains(path, StringComparison.Ordinal));
        var url = LinkPattern().Matches(message.TextBody).Select(m => m.Value).Last(u => u.Contains(path, StringComparison.Ordinal));
        return QueryHelpers.ParseQuery(new Uri(url).Query).ToDictionary(k => k.Key, v => v.Value.ToString());
    }

    [GeneratedRegex(@"https://\S+")]
    private static partial Regex LinkPattern();
}
