using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Farm_App.Data;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Services;

public sealed class FarmAlertMonitorService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory clients,
    ILogger<FarmAlertMonitorService> logger) : BackgroundService
{
    private static readonly string[] NewsFeeds =
    [
        "https://www.namibiansun.com/rssFeed/113",
        "https://namibianfarming.com/feed/",
        "https://thebrief.com.na/feed/",
        "https://www.namibian.com.na/feed/"
    ];

    private static readonly string[] HighImpactTerms =
    [
        "foot-and-mouth", "foot and mouth", "fmd", "quarantine", "movement ban",
        "livestock restriction", "animal disease outbreak", "disease outbreak",
        "anthrax", "lumpy skin", "avian influenza", "bird flu", "drought emergency",
        "livestock export ban", "meatco", "abattoir closure", "feed shortage",
        "livestock market", "meat price", "cattle price", "sheep price"
    ];

    private static readonly string[] AgricultureTerms =
    [
        "livestock", "cattle", "beef", "sheep", "goat", "meatco", "abattoir",
        "animal health", "veterinary", "animal feed", "feed price", "drought",
        "grazing", "auction", "meat price", "livestock price"
    ];
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    private async Task RunOnceAsync(CancellationToken token)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var push = scope.ServiceProvider.GetRequiredService<WebPushService>();

            await CheckThunderAsync(db, push, token);
            await CheckMeatPricesAsync(db, push, token);
            await CheckFuelAndNewsAsync(db, push, token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Farm alert monitor cycle failed");
        }
    }

    private async Task CheckThunderAsync(ApplicationDbContext db, WebPushService push, CancellationToken token)
    {
        var subscriptions = await db.FarmPushSubscriptions
            .Where(x => x.Latitude.HasValue && x.Longitude.HasValue)
            .ToListAsync(token);

        foreach (var sub in subscriptions)
        {
            try
            {
                var lat = sub.Latitude!.Value.ToString(CultureInfo.InvariantCulture);
                var lon = sub.Longitude!.Value.ToString(CultureInfo.InvariantCulture);
                var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&timezone=auto&forecast_days=2&hourly=weather_code";
                var client = clients.CreateClient("WeatherForecast");
                using var doc = JsonDocument.Parse(await client.GetStringAsync(url, token));
                var times = doc.RootElement.GetProperty("hourly").GetProperty("time");
                var codes = doc.RootElement.GetProperty("hourly").GetProperty("weather_code");
                for (var i = 0; i < times.GetArrayLength(); i++)
                {
                    var code = codes[i].GetInt32();
                    if (code is not (95 or 96 or 99)) continue;
                    var time = times[i].GetString();
                    if (string.IsNullOrWhiteSpace(time)) continue;

                    var key = $"thunder:{time}:{code}";
                    if (sub.LastThunderAlertKey == key) break;

                    await push.SendToUserAsync(
                        sub.OwnerId,
                        "FARM FIRE ALERT: Thunder expected",
                        $"Thunderstorm activity is forecast near your saved area around {time.Replace('T', ' ')}. Check fire conditions and prepare equipment.",
                        "/Conditions");

                    sub.LastThunderAlertKey = key;
                    sub.UpdatedAtUtc = DateTime.UtcNow;
                    break;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Thunder forecast check failed for subscription {SubscriptionId}", sub.Id);
            }
        }

        await db.SaveChangesAsync(token);
    }

    private async Task CheckMeatPricesAsync(ApplicationDbContext db, WebPushService push, CancellationToken token)
    {
        var client = clients.CreateClient("AuctionFeed");
        client.Timeout = TimeSpan.FromSeconds(25);
        var html = await client.GetStringAsync("https://www.feedmaster.com.na/meat-prices", token);
        var numbers = Regex.Matches(html, @"<td\b[^>]*>\s*([0-9]+(?:[.,][0-9]+)?)\s*</td>", RegexOptions.IgnoreCase)
            .Select(m => m.Groups[1].Value.Replace(",", "."))
            .Where(x => decimal.TryParse(x, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            .ToArray();
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join("|", numbers))));

        var settings = await db.PushServerSettings.SingleAsync(x => x.Id == 1, token);
        var state = ReadState(settings.AlertStateJson);
        if (state.TryGetValue("meatFingerprint", out var previous) && previous != fingerprint)
        {
            await SendToAllAsync(db, push,
                "FARM ALERT: Meat prices changed",
                "Published Feedmaster meat-price data has changed. Open Market & Calculators to review the latest beef and mutton prices.",
                "/Market");
        }
        state["meatFingerprint"] = fingerprint;
        settings.AlertStateJson = JsonSerializer.Serialize(state);
        await db.SaveChangesAsync(token);
    }
    private async Task CheckFuelAndNewsAsync(ApplicationDbContext db, WebPushService push, CancellationToken token)
    {
        var client = clients.CreateClient("AuctionFeed");
        var settings = await db.PushServerSettings.SingleAsync(x => x.Id == 1, token);
        var initialState = ReadState(settings.AlertStateJson);
        var firstRun = !initialState.ContainsKey("newsAlertsInitialized");
        if (firstRun)
        {
            initialState["newsAlertsInitialized"] = DateTime.UtcNow.ToString("O");
            settings.AlertStateJson = JsonSerializer.Serialize(initialState);
            await db.SaveChangesAsync(token);
        }

        foreach (var feed in NewsFeeds)
        {
            try
            {
                using var response = await client.GetAsync(feed, token);
                response.EnsureSuccessStatusCode();
                var xml = await response.Content.ReadAsStringAsync(token);
                var doc = XDocument.Parse(xml);
                foreach (var item in doc.Descendants().Where(x => x.Name.LocalName is "item" or "entry").Take(40))
                {
                    var title = Clean(item.Elements().FirstOrDefault(x => x.Name.LocalName == "title")?.Value);
                    var summary = Clean(item.Elements().FirstOrDefault(x => x.Name.LocalName is "description" or "summary" or "content")?.Value);
                    var link = item.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Value
                        ?? item.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Attribute("href")?.Value;
                    if (string.IsNullOrWhiteSpace(link)) continue;

                    var text = $"{title} {summary}".ToLowerInvariant();
                    var isFuel = text.Contains("fuel price") || text.Contains("petrol price") || text.Contains("diesel price");
                    var isImportant = HighImpactTerms.Any(text.Contains) && AgricultureTerms.Any(text.Contains);

                    if (!isFuel && !isImportant) continue;

                    var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(link))).ToLowerInvariant();
                    var alertSettings = await db.PushServerSettings.SingleAsync(x => x.Id == 1, token);
                    var state = ReadState(alertSettings.AlertStateJson);
                    var stateKey = isFuel ? $"fuel:{key}" : $"news:{key}";
                    if (state.ContainsKey(stateKey)) continue;
                    if (firstRun)
                    {
                        state[stateKey] = DateTime.UtcNow.ToString("O");
                        alertSettings.AlertStateJson = JsonSerializer.Serialize(state);
                        await db.SaveChangesAsync(token);
                        continue;
                    }

                    var notificationTitle = isFuel
                        ? "FARM ALERT: Fuel price update"
                        : "FARM ALERT: Important livestock news";
                    var body = isFuel
                        ? $"{title}. Open Fuel Prices to review the latest Namibia fuel-price announcement."
                        : $"{title}. This story may affect livestock markets, animal health, movement, or farm costs.";

                    await SendToAllAsync(db, push, notificationTitle, body, isFuel ? "/FuelPrices" : link);
                    state[stateKey] = DateTime.UtcNow.ToString("O");
                    TrimState(state);
                    alertSettings.AlertStateJson = JsonSerializer.Serialize(state);
                    await db.SaveChangesAsync(token);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "News feed alert check failed for {Feed}", feed);
            }
        }
    }

    private static Dictionary<string, string> ReadState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
        catch { return new Dictionary<string, string>(); }
    }

    private static void TrimState(Dictionary<string, string> state)
    {
        foreach (var key in state.OrderBy(x => x.Value).Take(Math.Max(0, state.Count - 120)).Select(x => x.Key).ToList())
            state.Remove(key);
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return Regex.Replace(WebUtility.HtmlDecode(value), @"\s+", " ").Trim();
    }
    private static async Task SendToAllAsync(
        ApplicationDbContext db,
        WebPushService push,
        string title,
        string body,
        string url)
    {
        var ownerIds = await db.FarmPushSubscriptions
            .AsNoTracking()
            .Select(x => x.OwnerId)
            .Distinct()
            .ToListAsync();

        foreach (var ownerId in ownerIds)
        {
            try
            {
                await push.SendToUserAsync(ownerId, title, body, url);
            }
            catch
            {
                // One user's expired subscription must not block other farm alerts.
            }
        }
    }
}
