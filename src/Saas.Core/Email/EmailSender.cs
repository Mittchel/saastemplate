using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using Saas.Core.Options;

namespace Saas.Core.Email;

/// <summary>Sends transactional email.</summary>
public interface IEmailSender
{
    Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// SMTP sender.
/// </summary>
/// <remarks>
/// Points at Mailpit locally, which accepts anything and shows it at
/// <c>http://localhost:8025</c> -- so the password-reset and invitation flows
/// are fully exercisable in development without a provider account. For
/// production, change <c>Mail__Host</c> and supply credentials; nothing in the
/// calling code changes.
/// </remarks>
public class SmtpEmailSender(
    IOptions<MailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly MailOptions _options = options.Value;

    /// <inheritdoc />
    public async Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage
        {
            Subject = subject,
            Body = new TextPart(TextFormat.Plain) { Text = body },
        };

        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));

        try
        {
            using var client = new SmtpClient();

            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("Sent {Subject} to {Recipient}", subject, toAddress);
        }
        catch (Exception exception)
        {
            // Logged and swallowed on purpose. A caller like the forgot-password
            // endpoint must return the same response whether or not the address
            // exists, and a failure here would otherwise turn a dead mail server
            // into an account-enumeration oracle.
            logger.LogError(
                exception, "Could not send {Subject} to {Recipient}", subject, toAddress);
        }
    }
}
