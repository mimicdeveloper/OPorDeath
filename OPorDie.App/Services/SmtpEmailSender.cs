using Microsoft.AspNetCore.Identity.UI.Services;
using SendGrid;
using SendGrid.Helpers.Mail;

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
        var apiKey = _config["SendGrid:ApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            _log.LogWarning("SendGrid API key not configured — skipping email to {Email}.", email);
            return;
        }

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(_config["Email:User"] ?? "noreply@opordie.com", "OP or Death");
        var to = new EmailAddress(email);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlMessage);

        var response = await client.SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
            _log.LogWarning("SendGrid returned {Status} for {Email}.", response.StatusCode, email);
    }
}
