using System.Text.Json;
using Farm_App.Models;
using Farm_App.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Farm_App.Controllers;

[Authorize]
public class AuctionsController : Controller
{
    private readonly string _folder;
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string UserFile => Path.Combine(_folder, $"auctions-{CurrentUserId}.json");
    private readonly AuctionFeedService _feed;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AuctionsController(IWebHostEnvironment env, AuctionFeedService feed)
    {
        _feed = feed;
        _folder = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(_folder);
    }

    private async Task<List<AuctionEvent>> LoadAsync()
    {
        await Gate.WaitAsync();
        try
        {
            var file = UserFile;
            if (!System.IO.File.Exists(file)) return new();
            await using var stream = System.IO.File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync<List<AuctionEvent>>(stream) ?? new();
        }
        finally { Gate.Release(); }
    }

    private async Task SaveAsync(List<AuctionEvent> events)
    {
        await Gate.WaitAsync();
        try
        {
            await using var stream = System.IO.File.Create(UserFile);
            await JsonSerializer.SerializeAsync(stream, events, JsonOptions);
        }
        finally { Gate.Release(); }
    }

    public async Task<IActionResult> Index(int? year, int? month)
    {
        var now = DateTime.Today;
        var selected = new DateTime(year ?? now.Year, month ?? now.Month, 1);
        ViewBag.MonthStart = selected;
        ViewBag.Previous = selected.AddMonths(-1);
        ViewBag.Next = selected.AddMonths(1);
        await _feed.RefreshAsync();
        var manual = await LoadAsync();
        var events = manual.Concat(_feed.GetEvents()).OrderBy(x => x.Date).ToList();
        ViewBag.FeedNotice = _feed.GetNotice();
        ViewBag.FeedUpdated = _feed.GetLastUpdated();
        ViewBag.FeedCount = _feed.GetEvents().Count;
        ViewBag.UpcomingCount = events.Count(x => x.Date.Date >= now);
        return View(events);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AuctionEvent item)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Please complete the required auction name and date, and check the website URL."; return RedirectToAction(nameof(Index), new { year = item.Date.Year, month = item.Date.Month }); }
        var events = await LoadAsync();
        item.Id = Guid.NewGuid();
        events.Add(item);
        await SaveAsync(events);
        TempData["Message"] = "Auction added to your calendar.";
        return RedirectToAction(nameof(Index), new { year = item.Date.Year, month = item.Date.Month });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var events = await LoadAsync();
        var item = events.FirstOrDefault(x => x.Id == id && !x.IsImported);
        if (item != null) { events.Remove(item); await SaveAsync(events); TempData["Message"] = "Auction removed."; }
        return RedirectToAction(nameof(Index));
    }
}
