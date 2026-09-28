using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Farm_App.Areas.Identity.Pages.Account;

public class ConfirmEmailModel(UserManager<IdentityUser> userManager) : PageModel
{
    public string Message { get; private set; } = "";
    public bool Confirmed { get; private set; }

    public async Task OnGetAsync(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            Message = "The confirmation link is incomplete. Please request a new confirmation email.";
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            Message = "We couldn't find an account for this confirmation link.";
            return;
        }

        try
        {
            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await userManager.ConfirmEmailAsync(user, decodedCode);
            Confirmed = result.Succeeded;
            Message = result.Succeeded
                ? "Your email address has been confirmed. You can now sign in."
                : "This confirmation link is invalid or has expired. Please request a new confirmation email.";
        }
        catch (FormatException)
        {
            Message = "This confirmation link is malformed. Please request a new confirmation email.";
        }
    }
}
