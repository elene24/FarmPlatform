namespace FarmPlatform.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Log" (development: write emails to the console) or "Smtp".</summary>
    public string Provider { get; set; } = "Log";

    public string FromAddress { get; set; } = "no-reply@localhost";

    public string FromName { get; set; } = "FarmPlatform";

    public SmtpSettings Smtp { get; set; } = new();
}

public sealed class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
