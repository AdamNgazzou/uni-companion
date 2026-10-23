using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace UniCompanion.Infrastructure.Identity;

public sealed class EmailOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string Sender { get; set; } = "companion@localhost";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; } = true;
}

public interface IAccountEmail
{
    Task SendAsync(string address, string subject, string body, CancellationToken cancellationToken);
}

public sealed class SmtpAccountEmail(IOptions<EmailOptions> options) : IAccountEmail
{
    public async Task SendAsync(string address, string subject, string body, CancellationToken cancellationToken)
    {
        var config = options.Value;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config.Sender));
        message.To.Add(MailboxAddress.Parse(address));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(config.Host, config.Port,
            config.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        if (!string.IsNullOrWhiteSpace(config.Username))
            await smtp.AuthenticateAsync(config.Username, config.Password ?? throw new InvalidOperationException("Email password is required when a username is configured."), cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }
}
