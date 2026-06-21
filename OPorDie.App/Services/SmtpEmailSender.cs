using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace OPorDie.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> log)
    {
        _config = config;
        _log = log;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var host = _config["Email:Host"];
        var port = int.TryParse(_config["Email:Port"], out var p) ? p : 587;
        var user = _config["Email:User"];
        var pass = _config["Email:Pass"];
        var from = _config["Email:From"] ?? user;

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user))
        {
            _log.LogWarning("Email not configured — skipping send to {Email}. Set Email:Host/User/Pass in appsettings.", email);
            return;
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass)
        };

        var msg = new MailMessage(from!, email, subject, htmlMessage) { IsBodyHtml = true };
        await client.SendMailAsync(msg);
    }
}
