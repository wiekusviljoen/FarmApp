using System.Globalization;
using System.Text.Json;
using Farm_App.Data;
using Farm_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Controllers;

public class LivestockController(ApplicationDbContext db) : Controller
{
    private const double DefaultLatitude = -25.95;
    private const double DefaultLongitude = 18.05;

    public async Task<IActionResult> Index(string? q)
    {
        var animals = db.Livestock.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            animals = animals.Where(a => a.TagNumber.Contains(q) || a.Species.Contains(q) || (a.Camp != null && a.Camp.Contains(q)));
        ViewBag.Query = q;
        var allActive = await db.Livestock.AsNoTracking().Where(a => a.Status == "Active").ToListAsync();
        ViewBag.ActiveCount = allActive.Count;
        ViewBag.ManualRain30 = await db.RainfallRecords.AsNoTracking().Where(r => r.Date >= DateTime.Today.AddDays(-30)).SumAsync(r => (decimal?)r.Millimeters) ?? 0m;
        ViewBag.SpeciesCountsJson = JsonSerializer.Serialize(allActive.GroupBy(a => a.Species).OrderBy(g => g.Key).Select(g => new { species = g.Key, count = g.Count() }));
        ViewBag.RainfallEntries = await db.RainfallRecords.AsNoTracking().OrderByDescending(r => r.Date).ThenBy(r => r.Camp).Take(100).ToListAsync();
        return View(await animals.OrderBy(a => a.Species).ThenBy(a => a.TagNumber).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LogRainfall(DateTime date, string? camp, decimal millimeters, string? notes)
    {
        if (date == default || millimeters < 0 || millimeters > 2000)
        {
            TempData["RainError"] = "Enter a valid date and rainfall amount between 0 and 2,000 mm.";
            return RedirectToAction(nameof(Index));
        }
        db.RainfallRecords.Add(new RainfallRecord { Date = date.Date, Camp = string.IsNullOrWhiteSpace(camp) ? null : camp.Trim(), Millimeters = millimeters, Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim() });
        await db.SaveChangesAsync();
        TempData["Message"] = "Rainfall entry saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRainfall(int id)
    {
        var record = await db.RainfallRecords.FindAsync(id);
        if (record != null) { db.RainfallRecords.Remove(record); await db.SaveChangesAsync(); }
        TempData["Message"] = "Rainfall entry deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> RainfallHistory(string period = "1y", double? latitude = null, double? longitude = null)
    {
        var months = period switch { "1m" => 1, "3m" => 3, "6m" => 6, "1y" => 12, "2y" => 24, "5y" => 60, _ => 12 };
        var hasLocation = latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
        var lat = hasLocation ? latitude!.Value : DefaultLatitude;
        var lon = hasLocation ? longitude!.Value : DefaultLongitude;
        var end = DateTime.UtcNow.Date.AddDays(-1);
        var start = end.AddMonths(-months).AddDays(1);
        var url = $"https://archive-api.open-meteo.com/v1/archive?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}&daily=precipitation_sum&timezone=auto";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var daily = doc.RootElement.GetProperty("daily");
            var dates = daily.GetProperty("time");
            var rain = daily.GetProperty("precipitation_sum");
            var records = Enumerable.Range(0, dates.GetArrayLength()).Select(i => new { date = dates[i].GetString(), rainMm = rain[i].ValueKind == JsonValueKind.Number ? rain[i].GetDouble() : 0d }).ToList();
            return Json(new { period, startDate = start.ToString("yyyy-MM-dd"), endDate = end.ToString("yyyy-MM-dd"), location = hasLocation ? "Detected location" : "Koes area (approximate fallback)", source = "Open-Meteo Historical Archive", records });
        }
        catch (Exception ex) { return StatusCode(502, new { error = "Could not load historical rainfall: " + ex.Message }); }
    }

    [HttpGet]
    public async Task<IActionResult> RainfallWeather(double? latitude, double? longitude)
    {
        var hasLocation = latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
        var lat = hasLocation ? latitude!.Value : DefaultLatitude;
        var lon = hasLocation ? longitude!.Value : DefaultLongitude;
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&timezone=auto&past_days=30&forecast_days=7&daily=precipitation_sum";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var daily = doc.RootElement.GetProperty("daily");
            var dates = daily.GetProperty("time");
            var rain = daily.GetProperty("precipitation_sum");
            var days = Enumerable.Range(0, dates.GetArrayLength()).Select(i => new { date = dates[i].GetString(), rainMm = rain[i].GetDouble(), isForecast = i >= dates.GetArrayLength() - 7 }).ToList();
            var past = days.Where(d => !d.isForecast).Sum(d => d.rainMm);
            return Json(new { source = "Open-Meteo", location = hasLocation ? "Detected location" : "Koes area (approximate fallback)", fetchedAt = DateTimeOffset.Now, past30DaysMm = Math.Round(past, 1), days });
        }
        catch (Exception ex) { return StatusCode(502, new { error = "Could not load rainfall data: " + ex.Message }); }
    }

    public IActionResult Create() => View(new Livestock());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Livestock animal)
    {
        if (!ModelState.IsValid) return View(animal);
        db.Add(animal); await db.SaveChangesAsync();
        TempData["Message"] = "Animal added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var animal = await db.Livestock.FindAsync(id);
        return animal == null ? NotFound() : View(animal);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Livestock animal)
    {
        if (id != animal.Id) return NotFound();
        if (!ModelState.IsValid) return View(animal);
        db.Update(animal); await db.SaveChangesAsync();
        TempData["Message"] = "Animal updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var animal = await db.Livestock.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return animal == null ? NotFound() : View(animal);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var animal = await db.Livestock.FindAsync(id);
        if (animal != null) { db.Livestock.Remove(animal); await db.SaveChangesAsync(); }
        TempData["Message"] = "Animal record deleted.";
        return RedirectToAction(nameof(Index));
    }
}
