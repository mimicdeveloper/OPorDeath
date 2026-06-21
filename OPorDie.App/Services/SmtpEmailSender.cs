using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace OPorDie.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IConfiguration config, IHttpClientFactory http, ILogger<SmtpEmailSender> log)
    {
        _config = config;
        _http = http;
        _log = log;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
    {
        var apiKey = _config["Resend:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _log.LogWarning("Resend API key not configured — skipping email to {Email}.", toEmail);
            return;
        }

        var client = _http.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var payload = new
        {
            from = "OP or Death <onboarding@resend.dev>",
            to = new[] { toEmail },
            subject,
            html = Wrap(subject, htmlMessage)
        };

        var json = JsonSerializer.Serialize(payload);
        var response = await client.PostAsync(
            "https://api.resend.com/emails",
            new StringContent(json, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _log.LogWarning("Resend returned {Status}: {Body}", response.StatusCode, body);
        }
    }

    private static string Wrap(string subject, string body) => $"""
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8"/>
          <meta name="viewport" content="width=device-width,initial-scale=1"/>
          <title>{subject}</title>
        </head>
        <body style="margin:0;padding:0;background:#0a0a0d;font-family:'Segoe UI',Arial,sans-serif;color:#ece6dc">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#0a0a0d;padding:32px 16px">
            <tr><td align="center">
              <table width="560" cellpadding="0" cellspacing="0" style="max-width:560px;width:100%">

                <tr>
                  <td style="background:linear-gradient(135deg,#16131a,#1d1820);border-radius:14px 14px 0 0;
                             border:1px solid #2c2230;border-bottom:none;padding:28px 32px;text-align:center">
                    <div style="font-size:2.2rem;font-weight:900;letter-spacing:1px;
                                color:#e9b84c;text-shadow:0 2px 0 #000">
                      OP or Death ☠
                    </div>
                    <div style="width:80px;height:3px;margin:10px auto 0;
                                background:linear-gradient(90deg,#c01826,#e9b84c);border-radius:3px"></div>
                  </td>
                </tr>

                <tr>
                  <td style="background:#16131a;border:1px solid #2c2230;border-top:none;border-bottom:none;
                             padding:32px 32px 24px;color:#ece6dc;font-size:1rem;line-height:1.6">
                    {body}
                  </td>
                </tr>

                <tr>
                  <td style="background:#0e0b10;border:1px solid #2c2230;border-top:none;
                             border-radius:0 0 14px 14px;padding:16px 32px;text-align:center">
                    <p style="margin:0;font-size:.78rem;color:#6b6060">
                      You're receiving this because you signed up at OP or Death.<br/>
                      Card art © Eiichiro Oda / Shueisha / Toei / Bandai — fan project, not affiliated with Bandai.
                    </p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
