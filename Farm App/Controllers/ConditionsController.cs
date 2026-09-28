using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Farm_App.Controllers;

public class ConditionsController : Controller
{
    private const double DefaultLatitude = -25.95;
    private const double DefaultLongitude = 18.05;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Forecast(double? latitude, double? longitude)
    {
        var hasLocation = latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
        var lat = hasLocation ? latitude!.Value : DefaultLatitude;
        var lon = hasLocation ? longitude!.Value : DefaultLongitude;
        var source = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&timezone=auto&forecast_days=7&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,wind_speed_10m_max&current=temperature_2m,relative_humidity_2m,wind_speed_10m,precipitation";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var json = await client.GetStringAsync(source);
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

    private static async Task<string> GetLocationNameAsync(double latitude, double longitude)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FarmApp/1.0 (weather location lookup)");
            var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&zoom=10&addressdetails=1";
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return "Your detected area";
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("address", out var address)) return "Your detected area";
            foreach (var key in new[] { "state", "region", "county", "city", "town", "village", "municipality" })
                if (address.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString()!;
            return "Your detected area";
        }
        catch { return "Your detected area"; }
    }
}
