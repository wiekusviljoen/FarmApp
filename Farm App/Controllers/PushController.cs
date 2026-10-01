using System.Security.Claims;
using Farm_App.Data;
using Farm_App.Models;
using Farm_App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class PushController(ApplicationDbContext db, WebPushService webPush) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> PublicKey()
    {
        var settings = await db.PushServerSettings.AsNoTracking().SingleAsync(x => x.Id == 1);
        return Json(new { publicKey = settings.PublicKey });
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint) ||
            string.IsNullOrWhiteSpace(request.P256dh) ||
            string.IsNullOrWhiteSpace(request.Auth))
            return BadRequest(new { error = "Incomplete push subscription." });

        var existing = await db.FarmPushSubscriptions
            .FirstOrDefaultAsync(x => x.OwnerId == CurrentUserId && x.Endpoint == request.Endpoint);

        if (existing is null)
        {
            db.FarmPushSubscriptions.Add(new FarmPushSubscription
            {
                OwnerId = CurrentUserId,
                Endpoint = request.Endpoint.Trim(),
                P256dh = request.P256dh.Trim(),
                Auth = request.Auth.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude
            });
        }
        else
        {
            existing.P256dh = request.P256dh.Trim();
            existing.Auth = request.Auth.Trim();
            existing.Latitude = request.Latitude;
            existing.Longitude = request.Longitude;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { subscribed = true });
    }

    [HttpPost]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint)) return BadRequest();
        var item = await db.FarmPushSubscriptions
            .FirstOrDefaultAsync(x => x.OwnerId == CurrentUserId && x.Endpoint == request.Endpoint);
        if (item is not null)
        {
            db.FarmPushSubscriptions.Remove(item);
            await db.SaveChangesAsync();
        }
        return Ok(new { subscribed = false });
    }

    [HttpPost]
    public async Task<IActionResult> Test()
    {
        await webPush.SendTestAsync(CurrentUserId);
        return Ok(new { sent = true });
    }
}

public sealed record PushSubscriptionRequest(string Endpoint, string P256dh, string Auth, double? Latitude, double? Longitude);
public sealed record PushUnsubscribeRequest(string Endpoint);
