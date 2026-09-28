using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Text.Encodings.Web;

namespace Farm_App.Services;

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender, IEmailSender<IdentityUser>
{
    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink) =>
        SendEmailAsync(email, "Confirm your Farm App account", $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>clicking here</a>.");

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
