namespace FarmPlatform.Infrastructure.Sms;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// "Log" prints messages to the console (development). A real provider (e.g. a Georgian SMS gateway or
    /// Twilio) is added as another ISmsSender implementation and selected here before going live.
    /// </summary>
    public string Provider { get; set; } = "Log";
}
