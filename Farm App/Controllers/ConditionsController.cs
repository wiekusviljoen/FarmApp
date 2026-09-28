using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Farm_App.Controllers;

public class ConditionsController : Controller
{
    // Approximate Koes town coordinates; replace with the farm's exact pin for better local forecasts.
    private const double Latitude = -25.95;
    private const double Longitude = 18.05;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Forecast()
    {
        const string source = "https://api.open-meteo.com/v1/forecast?latitude=-25.95&longitude=18.05&timezone=Africa%2FWindhoek&forecast_days=7&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,wind_speed_10m_max&current=temperature_2m,relative_humidity_2m,wind_speed_10m,precipitation";
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
                days.Add(new {
                    date = dates[i].GetString(),
                    maxTemp = daily.GetProperty("temperature_2m_max")[i].GetDouble(),
                    minTemp = daily.GetProperty("temperature_2m_min")[i].GetDouble(),
                    rainMm = daily.GetProperty("precipitation_sum")[i].GetDouble(),
                    rainChance = daily.GetProperty("precipitation_probability_max")[i].GetInt32(),
                    windKmh = daily.GetProperty("wind_speed_10m_max")[i].GetDouble()
                });
            var current = root.GetProperty("current");
            return Json(new {
                source = "Open-Meteo forecast",
                fetchedAt = DateTimeOffset.Now,
                current = new {
                    temp = current.GetProperty("temperature_2m").GetDouble(),
                    humidity = current.GetProperty("relative_humidity_2m").GetInt32(),
                    windKmh = current.GetProperty("wind_speed_10m").GetDouble(),
                    rainMm = current.GetProperty("precipitation").GetDouble()
                }, days
            });
        }
        catch (Exception ex) { return StatusCode(502, new { error = "Forecast unavailable: " + ex.Message }); }
    }
}
