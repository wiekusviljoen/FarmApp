using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Text.Encodings.Web;

namespace Farm_App.Services;

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender, IEmailSender<IdentityUser>
{
    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
    {
        var safeLink = HtmlEncoder.Default.Encode(confirmationLink);
        var html = $@"
<!doctype html>
<html lang='en'>
<head>
  <meta charset='utf-8'>
  <meta name='viewport' content='width=device-width,initial-scale=1'>
  <title>Confirm your Farm account</title>
</head>
<body style='margin:0;padding:0;background:#eef2ed;font-family:Arial,Helvetica,sans-serif;color:#243229;'>
  <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background:#eef2ed;padding:32px 12px;'>
    <tr><td align='center'>
      <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='max-width:620px;background:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 8px 30px rgba(25,55,35,.12);'>
        <tr>
          <td style='background:#183d2a;padding:30px 36px;text-align:center;'>
            <div style='font-size:30px;font-weight:800;letter-spacing:-1px;color:#ffffff;'>Farm</div>
            <div style='margin-top:7px;font-size:13px;color:#cbdccf;letter-spacing:.5px;'>SMART FARM MANAGEMENT</div>
          </td>
        </tr>
        <tr>
          <td style='padding:42px 40px 34px;'>
            <div style='font-size:12px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase;color:#64816d;margin-bottom:12px;'>Welcome to Farm</div>
            <h1 style='margin:0 0 16px;font-size:30px;line-height:1.2;color:#183d2a;'>Confirm your email address</h1>
            <p style='margin:0 0 24px;font-size:16px;line-height:1.65;color:#526158;'>Thanks for creating your Farm account. Confirm your email address to finish setting up your account and start managing your farm from one place.</p>
            <table role='presentation' cellpadding='0' cellspacing='0' style='margin:0 auto 28px;'>
              <tr><td align='center' style='border-radius:10px;background:#2f6b45;'>
                <a href='{safeLink}' style='display:inline-block;padding:15px 30px;font-size:16px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:10px;'>Confirm my email</a>
              </td></tr>
            </table>
            <div style='border-top:1px solid #e3e9e4;padding-top:22px;'>
              <p style='margin:0 0 8px;font-size:12px;font-weight:700;color:#526158;'>Button not working?</p>
              <p style='margin:0;font-size:12px;line-height:1.6;color:#78847c;word-break:break-all;'>{safeLink}</p>
            </div>
            <div style='margin-top:26px;padding:15px 16px;background:#f5f8f5;border-radius:10px;'>
              <p style='margin:0;font-size:12px;line-height:1.6;color:#68756d;'>If you did not create a Farm account, you can safely ignore this email.</p>
            </div>
          </td>
        </tr>
        <tr>
          <td style='padding:20px 36px;background:#f7f9f7;text-align:center;border-top:1px solid #e5ebe6;'>
            <div style='font-size:12px;color:#78847c;'>Farm &bull; Your farm, managed smarter.</div>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";

        return SendEmailAsync(email, "Confirm your Farm account", html);
    }

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink) =>
        SendEmailAsync(email, "Reset your Farm App password", $"Reset your password by <a href='{HtmlEncoder.Default.Encode(resetLink)}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode) =>
        SendEmailAsync(email, "Your Farm App password reset code", $"Your password reset code is: <strong>{HtmlEncoder.Default.Encode(resetCode)}</strong>");

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var section = configuration.GetSection("Email");
        var host = section["SmtpHost"];
        var from = section["From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Email is not configured. Set Email:SmtpHost and Email:From (plus credentials if required) in user-secrets or environment variables.");

        var port = int.TryParse(section["SmtpPort"], out var parsedPort) ? parsedPort : 587;
        var username = section["Username"];
        var password = section["Password"];
        var useSsl = bool.TryParse(section["EnableSsl"], out var parsedSsl) ? parsedSsl : true;

        using var message = new MailMessage
        {
            From = new MailAddress(from, section["DisplayName"] ?? "Farm App"),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        message.To.Add(MailAddressCollectionCheck(email));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password ?? string.Empty);

        try
        {
            await client.SendMailAsync(message);
            logger.LogInformation("Account email sent successfully to {Recipient}.", email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send account email to {Recipient}.", email);
            throw;
        }
    }

    private static string MailAddressCollectionCheck(string email) => new MailAddress(email).Address;
}
