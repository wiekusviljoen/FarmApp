using System.Text.Json;
using Farm_App.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace Farm_App.Services;

public sealed class WebPushService(ApplicationDbContext db, ILogger<WebPushService> logger)
{
    public async Task SendToUserAsync(string ownerId, string title, string body, string? url = null)
    {
        var settings = await db.PushServerSettings.AsNoTracking().SingleAsync(x => x.Id == 1);
        var subscriptions = await db.FarmPushSubscriptions
            .Where(x => x.OwnerId == ownerId)
            .ToListAsync();

        var payload = JsonSerializer.Serialize(new { title, body, url = url ?? "/" });
        var client = new WebPushClient();
        var vapid = new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey);

        foreach (var item in subscriptions)
        {
            try
            {
                var subscription = new WebPush.PushSubscription(item.Endpoint, item.P256dh, item.Auth);
                await client.SendNotificationAsync(subscription, payload, vapid);
            }
            catch (WebPushException ex) when ((int?)ex.StatusCode is 404 or 410)
            {
                db.FarmPushSubscriptions.Remove(item);
                logger.LogInformation("Removed expired web push subscription.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Web push delivery failed.");
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task SendTestAsync(string ownerId) =>
        await SendToUserAsync(ownerId, "Farm notification test", "Push notifications are working on your Farm app.", "/");
}
