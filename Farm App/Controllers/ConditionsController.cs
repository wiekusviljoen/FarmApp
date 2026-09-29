using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Farm_App.Controllers;

public class ConditionsController : Controller
{
    private const double DefaultLatitude = -25.95;
    private const double DefaultLongitude = 18.05;
    private readonly IHttpClientFactory _clients;
    private readonly IMemoryCache _cache;

    public ConditionsController(IHttpClientFactory clients, IMemoryCache cache)
    {
        _clients = clients;
        _cache = cache;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Forecast(double? latitude, double? longitude)
    {
        var hasLocation = latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
        var lat = hasLocation ? latitude!.Value : DefaultLatitude;
        var lon = hasLocation ? longitude!.Value : DefaultLongitude;
        var latKey = Math.Round(lat, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lonKey = Math.Round(lon, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var cacheKey = $"weather-forecast:{latKey}:{lonKey}";
        var source = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&timezone=auto&forecast_days=7&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,wind_speed_10m_max&current=temperature_2m,relative_humidity_2m,wind_speed_10m,precipitation";
        try
        {
            var client = _clients.CreateClient("WeatherForecast");
            var json = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await client.GetStringAsync(source);
            });
            if (json is null) return StatusCode(502, new { error = "Forecast unavailable." });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var daily = root.GetProperty("daily");
            var days = new List<object>();
            var dates = daily.GetProperty("time");
            for (var i = 0; i < dates.GetArrayLength(); i++)
                days.Add(new
                {
                    date = dates[i].GetString(),
                    maxTemp = daily.GetProperty("temperature_2m_max")[i].GetDouble(),
                    minTemp = daily.GetProperty("temperature_2m_min")[i].GetDouble(),
                    rainMm = daily.GetProperty("precipitation_sum")[i].GetDouble(),
                    rainChance = daily.GetProperty("precipitation_probability_max")[i].GetInt32(),
                    windKmh = daily.GetProperty("wind_speed_10m_max")[i].GetDouble()
                });
            var current = root.GetProperty("current");
            var locationName = hasLocation
                ? await GetLocationNameAsync(lat, lon)
                : "Koes area (approximate fallback)";
            return Json(new
            {
                source = "Open-Meteo forecast",
                fetchedAt = DateTimeOffset.Now,
                location = locationName,
                latitude = lat,
                longitude = lon,
                current = new
                {
                    temp = current.GetProperty("temperature_2m").GetDouble(),
                    humidity = current.GetProperty("relative_humidity_2m").GetInt32(),
                    windKmh = current.GetProperty("wind_speed_10m").GetDouble(),
                    rainMm = current.GetProperty("precipitation").GetDouble()
                }, days
            });
        }
        catch (Exception ex) { return StatusCode(502, new { error = "Forecast unavailable: " + ex.Message }); }
    }

    private async Task<string> GetLocationNameAsync(double latitude, double longitude)
    {
        var cacheKey = $"weather-location:{Math.Round(latitude, 2).ToString(System.Globalization.CultureInfo.InvariantCulture)}:{Math.Round(longitude, 2).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (_cache.TryGetValue(cacheKey, out string? cachedName) && !string.IsNullOrWhiteSpace(cachedName)) return cachedName;
        try
        {
            var client = _clients.CreateClient("WeatherGeocoding");
            var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&zoom=10&addressdetails=1";
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return "Your detected area";
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("address", out var address)) return "Your detected area";
            foreach (var key in new[] { "state", "region", "county", "city", "town", "village", "municipality" })
                if (address.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    var name = value.GetString()!;
                    _cache.Set(cacheKey, name, TimeSpan.FromHours(24));
                    return name;
                }
            return "Your detected area";
        }
        catch { return "Your detected area"; }
    }
}
