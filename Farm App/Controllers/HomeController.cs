using System.Diagnostics;
using System.Security.Claims;
using Farm_App.Data;
using Farm_App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return View("Welcome");

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var active = db.Livestock.AsNoTracking().Where(a => a.OwnerId == ownerId && a.Status == "Active");
        var model = new DashboardViewModel
        {
            ActiveAnimals = await active.CountAsync(),
            TotalAnimals = await db.Livestock.AsNoTracking().CountAsync(a => a.OwnerId == ownerId),
            BirthsThisMonth = await db.LivestockEvents.AsNoTracking().CountAsync(e => e.OwnerId == ownerId && e.EventType == "Birth" && e.EventDate >= monthStart && e.EventDate <= DateTime.Today),
            DeathsThisMonth = await db.LivestockEvents.AsNoTracking().CountAsync(e => e.OwnerId == ownerId && e.EventType == "Death" && e.EventDate >= monthStart && e.EventDate <= DateTime.Today),
            RainfallThisMonth = await db.RainfallRecords.AsNoTracking().Where(r => r.OwnerId == ownerId && r.Date >= monthStart).SumAsync(r => (decimal?)r.Millimeters) ?? 0m,
            CampCount = await active.Where(a => a.Camp != null && a.Camp != "").Select(a => a.Camp!).Distinct().CountAsync(),
            RecentEvents = await db.LivestockEvents.AsNoTracking().Where(e => e.OwnerId == ownerId).Include(e => e.Livestock).OrderByDescending(e => e.EventDate).ThenByDescending(e => e.Id).Take(6).ToListAsync(),
            SpeciesCounts = await active.GroupBy(a => a.Species).Select(g => new SpeciesCount { Species = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ToListAsync()
        };
        return View(model);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}