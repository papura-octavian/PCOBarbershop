using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PCOBarbershop.Server.Settings;

namespace PCOBarbershop.Server.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
}

// Trimite e-mailurile tranzactionale prin SMTP, cu MailKit
public class EmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpSettings> options, ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        // Fara server SMTP configurat: scriem e-mailul in log, ca aplicatia sa mearga si asa
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            _logger.LogWarning(
                "SMTP nu este configurat. E-mail catre {To}, subiect \"{Subject}\":\n{Body}",
                toEmail, subject, htmlBody);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var security = _settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

        await client.ConnectAsync(_settings.Host, _settings.Port, security);

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("E-mail trimis catre {To}: {Subject}", toEmail, subject);
    }
}
