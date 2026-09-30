using System.Globalization;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Farm_App.Data;
using Farm_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Controllers;

[Authorize]
public class LivestockController(ApplicationDbContext db) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private const double DefaultLatitude = -25.95;
    private const double DefaultLongitude = 18.05;

    public async Task<IActionResult> Index(string? q)
    {
        var animals = db.Livestock.AsNoTracking().Where(a => a.OwnerId == CurrentUserId);
        if (!string.IsNullOrWhiteSpace(q))
            animals = animals.Where(a => a.TagNumber.Contains(q) || a.Species.Contains(q) || (a.Camp != null && a.Camp.Contains(q)));
        ViewBag.Query = q;
        var allActive = await db.Livestock.AsNoTracking().Where(a => a.OwnerId == CurrentUserId && a.Status == "Active").ToListAsync();
        ViewBag.ActiveCount = allActive.Count;
        ViewBag.ManualRain30 = await db.RainfallRecords.AsNoTracking().Where(r => r.OwnerId == CurrentUserId && r.Date >= DateTime.Today.AddDays(-30)).SumAsync(r => (decimal?)r.Millimeters) ?? 0m;
        ViewBag.SpeciesCountsJson = JsonSerializer.Serialize(allActive.GroupBy(a => a.Species).OrderBy(g => g.Key).Select(g => new { species = g.Key, count = g.Count() }));
        ViewBag.RainfallEntries = await db.RainfallRecords.AsNoTracking().Where(r => r.OwnerId == CurrentUserId).OrderByDescending(r => r.Date).ThenBy(r => r.Camp).Take(100).ToListAsync();
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
        db.RainfallRecords.Add(new RainfallRecord { OwnerId = CurrentUserId, Date = date.Date, Camp = string.IsNullOrWhiteSpace(camp) ? null : camp.Trim(), Millimeters = millimeters, Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim() });
        await db.SaveChangesAsync();
        TempData["Message"] = "Rainfall entry saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRainfall(int id)
    {
        var record = await db.RainfallRecords.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == CurrentUserId);
        if (record != null) { db.RainfallRecords.Remove(record); await db.SaveChangesAsync(); }
        TempData["Message"] = "Rainfall entry deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> RainfallHistory(string period = "1y", double? latitude = null, double? longitude = null)
    {
        var isSeason = period.Equals("season", StringComparison.OrdinalIgnoreCase);
        var seasonStart = DateTime.Today.Month >= 10
            ? new DateTime(DateTime.Today.Year, 10, 1)
            : new DateTime(DateTime.Today.Year - 1, 10, 1);
        var seasonEnd = DateTime.Today.Month >= 10
            ? new DateTime(DateTime.Today.Year + 1, 7, 31)
            : new DateTime(DateTime.Today.Year, 7, 31);
        var months = period switch { "1m" => 1, "3m" => 3, "6m" => 6, "1y" => 12, "2y" => 24, "5y" => 60, _ => 12 };
        var hasLocation = latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
        var lat = hasLocation ? latitude!.Value : DefaultLatitude;
        var lon = hasLocation ? longitude!.Value : DefaultLongitude;
        var end = isSeason ? seasonEnd : DateTime.UtcNow.Date.AddDays(-1);
        if (end >= DateTime.UtcNow.Date) end = DateTime.UtcNow.Date.AddDays(-1);
        var start = isSeason ? seasonStart : end.AddMonths(-months).AddDays(1);
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
            return Json(new { period, season = isSeason ? $"{seasonStart:yyyy}/{seasonEnd:yy}" : null, startDate = start.ToString("yyyy-MM-dd"), endDate = end.ToString("yyyy-MM-dd"), location = hasLocation ? "Detected location" : "Koes area (approximate fallback)", source = "Open-Meteo Historical Archive", records });
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

    public async Task<IActionResult> History(int id)
    {
        var animal = await db.Livestock.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == CurrentUserId);
        if (animal == null) return NotFound();
        ViewBag.Animal = animal;
        var events = await db.LivestockEvents.AsNoTracking()
            .Where(e => e.LivestockId == id && e.OwnerId == CurrentUserId)
            .OrderByDescending(e => e.EventDate).ThenByDescending(e => e.Id).ToListAsync();
        ViewBag.HealthCases = await db.AnimalHealthCases.AsNoTracking()
            .Where(h => h.LivestockId == id && h.OwnerId == CurrentUserId)
            .OrderByDescending(h => h.CreatedAt).Take(20).ToListAsync();
        ViewBag.EventCount = events.Count;
        ViewBag.BirthCount = events.Count(e => e.EventType == "Birth");
        ViewBag.TreatmentCount = events.Count(e => e.EventType == "Treatment" || e.EventType == "Vaccination");
        ViewBag.LastEventDate = events.Select(e => (DateTime?)e.EventDate).FirstOrDefault();
        ViewBag.AgeYears = animal.DateOfBirth.HasValue
            ? Math.Max(0, (DateTime.Today - animal.DateOfBirth.Value.Date).Days / 365.2425)
            : (double?)null;
        return View(events);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEvent(int livestockId, string eventType, DateTime eventDate, decimal? amount, string? notes)
    {
        var allowedTypes = new[] { "Birth", "Purchase", "Sale", "Death", "Movement", "Treatment", "Vaccination", "Other" };
        var animal = await db.Livestock.FirstOrDefaultAsync(a => a.Id == livestockId && a.OwnerId == CurrentUserId);
        if (animal == null) return NotFound();
        if (!allowedTypes.Contains(eventType) || eventDate == default || amount < 0 || amount > 100000000)
        {
            TempData["EventError"] = "Please enter a valid event type, date, and amount.";
            return RedirectToAction(nameof(History), new { id = livestockId });
        }
        db.LivestockEvents.Add(new LivestockEvent
        {
            OwnerId = CurrentUserId,
            LivestockId = livestockId,
            EventType = eventType,
            EventDate = eventDate.Date,
            Amount = amount,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
        await db.SaveChangesAsync();
        TempData["Message"] = "Livestock event saved.";
        return RedirectToAction(nameof(History), new { id = livestockId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var entry = await db.LivestockEvents.FirstOrDefaultAsync(e => e.Id == id && e.OwnerId == CurrentUserId);
        if (entry != null)
        {
            var livestockId = entry.LivestockId;
            db.LivestockEvents.Remove(entry);
            await db.SaveChangesAsync();
            TempData["Message"] = "Event deleted.";
            return RedirectToAction(nameof(History), new { id = livestockId });
        }
        return NotFound();
    }

    public IActionResult Create() => View(new Livestock());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Livestock animal)
    {
        if (!ModelState.IsValid) return View(animal);
        animal.OwnerId = CurrentUserId;
        db.Livestock.Add(animal); await db.SaveChangesAsync();
        TempData["Message"] = "Animal added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var animal = await db.Livestock.FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == CurrentUserId);
        if (animal == null) return NotFound();

        ViewBag.Camps = await db.FarmCamps.AsNoTracking()
            .Where(c => c.OwnerId == CurrentUserId)
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync();

        return View(animal);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Livestock animal)
    {
        if (id != animal.Id) return NotFound();
        var existing = await db.Livestock.FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == CurrentUserId);
        if (existing == null) return NotFound();

        var selectedCamp = string.IsNullOrWhiteSpace(animal.Camp) ? null : animal.Camp.Trim();
        if (selectedCamp != null)
        {
            var campExists = await db.FarmCamps.AnyAsync(c =>
                c.OwnerId == CurrentUserId && c.Name == selectedCamp);
            if (!campExists)
                ModelState.AddModelError(nameof(animal.Camp), "Please choose a camp from your camps list.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Camps = await db.FarmCamps.AsNoTracking()
                .Where(c => c.OwnerId == CurrentUserId)
                .OrderBy(c => c.Name)
                .Select(c => c.Name)
                .ToListAsync();
            return View(animal);
        }

        existing.TagNumber = animal.TagNumber;
        existing.Species = animal.Species;
        existing.Breed = animal.Breed;
        existing.Sex = animal.Sex;
        existing.DateOfBirth = animal.DateOfBirth;
        existing.Camp = selectedCamp;
        existing.Status = animal.Status;
        existing.PurchasePrice = animal.PurchasePrice;
        existing.Notes = animal.Notes;
        await db.SaveChangesAsync();
        TempData["Message"] = "Animal updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var animal = await db.Livestock.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == CurrentUserId);
        return animal == null ? NotFound() : View(animal);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var animal = await db.Livestock.FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == CurrentUserId);
        if (animal != null) { db.Livestock.Remove(animal); await db.SaveChangesAsync(); }
        TempData["Message"] = "Animal record deleted.";
        return RedirectToAction(nameof(Index));
    }
}
