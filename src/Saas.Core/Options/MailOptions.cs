namespace Saas.Core.Options;

/// <summary>SMTP settings. Points at Mailpit locally; swap the host in production.</summary>
public class MailOptions
{
    public const string SectionName = "Mail";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    public string FromAddress { get; set; } = "no-reply@saas.local";
    public string FromName { get; set; } = "Saas";
}
