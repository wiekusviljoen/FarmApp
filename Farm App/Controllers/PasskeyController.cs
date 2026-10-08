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
    public IActionResult Index()
    {
        return View();
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

        return Ok(new { success = true });
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("PasskeyRequestOptions")]
    public async Task<IActionResult> RequestOptions([FromForm] string? username)
    {
        IdentityUser? user = null;
        if (!string.IsNullOrWhiteSpace(username))
            user = await _userManager.FindByNameAsync(username);

        var optionsJson = await _signInManager.MakePasskeyRequestOptionsAsync(user);
        return Content(optionsJson, "application/json");
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("PasskeyLogin")]
    public async Task<IActionResult> Login([FromBody] PasskeyCredentialRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CredentialJson))
            return BadRequest("Missing passkey credential.");

        var result = await _signInManager.PasskeySignInAsync(request.CredentialJson);

        if (result.Succeeded)
            return Ok(new { success = true, redirect = Url.Content("~/") });

        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked, "Account is temporarily locked.");

        return Unauthorized("Fingerprint/passkey authentication failed.");
    }

    public sealed class PasskeyCredentialRequest
    {
        public string CredentialJson { get; set; } = string.Empty;
    }
}
