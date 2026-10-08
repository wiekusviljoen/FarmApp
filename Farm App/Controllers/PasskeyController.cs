using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Farm_App.Controllers;

[Route("Account")]
public class PasskeyController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public PasskeyController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [Authorize]
    [HttpGet("Passkey")]
    public IActionResult Index(string? returnUrl = null)
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        var isPhone = userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase);

        if (!isPhone)
            return LocalRedirect("~/");

        ViewData["ReturnUrl"] = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");
        return View();
    }

    [Authorize]
    [HttpGet("PasskeySkip")]
    public IActionResult Skip(string? returnUrl = null)
    {
        MarkPrompted();
        var destination = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");
        return LocalRedirect(destination!);
    }

    private void MarkPrompted()
    {
        Response.Cookies.Append("farm.passkey.prompted", "1", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddYears(5)
        });
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("PasskeyCreationOptions")]
    public async Task<IActionResult> CreationOptions()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var userId = await _userManager.GetUserIdAsync(user);
        var userName = await _userManager.GetUserNameAsync(user) ?? "Farm user";

        var optionsJson = await _signInManager.MakePasskeyCreationOptionsAsync(new()
        {
            Id = userId,
            Name = userName,
            DisplayName = userName
        });

        return Content(optionsJson, "application/json");
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("PasskeyRegistration")]
    public async Task<IActionResult> Registration([FromBody] PasskeyCredentialRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CredentialJson))
            return BadRequest("Missing passkey credential.");

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var result = await _signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
        if (!result.Succeeded)
            return BadRequest(result.Failure?.Message ?? "Passkey registration failed.");

        var addResult = await _userManager.AddOrUpdatePasskeyAsync(user, result.Passkey!);
        if (!addResult.Succeeded)
            return BadRequest(string.Join(" ", addResult.Errors.Select(e => e.Description)));

        MarkPrompted();
        return Ok(new { success = true });
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("PasskeyRequestOptions")]
    public async Task<IActionResult> RequestOptions()
    {
        // The phone is already signed in with its normal Farm session. Generate
        // request options for that exact account so Android only offers passkeys
        // registered to this Farm account, rather than unrelated discoverable keys.
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var optionsJson = await _signInManager.MakePasskeyRequestOptionsAsync(user);
        return Content(optionsJson, "application/json");
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("PasskeyLogin")]
    public async Task<IActionResult> Login([FromBody] PasskeyCredentialRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CredentialJson))
            return BadRequest("Missing passkey credential.");

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
            return Unauthorized("Your Farm session has expired. Please sign in again.");

        // Verify the assertion against the passkey options generated for the
        // current Farm account. This avoids ambiguous discoverable credentials
        // when the phone has more than one passkey for the same website.
        var result = await _signInManager.PerformPasskeyAssertionAsync(request.CredentialJson);
        if (!result.Succeeded || result.User is null)
            return Unauthorized("Fingerprint/passkey authentication failed.");

        var currentUserId = await _userManager.GetUserIdAsync(currentUser);
        var assertedUserId = await _userManager.GetUserIdAsync(result.User);
        if (!string.Equals(currentUserId, assertedUserId, StringComparison.Ordinal))
            return Unauthorized("That fingerprint belongs to a different Farm account.");

        var updateResult = await _userManager.AddOrUpdatePasskeyAsync(result.User, result.Passkey!);
        if (!updateResult.Succeeded)
            return BadRequest(string.Join(" ", updateResult.Errors.Select(e => e.Description)));

        return Ok(new { success = true });
    }

    public sealed class PasskeyCredentialRequest
    {
        public string CredentialJson { get; set; } = string.Empty;
    }
}
