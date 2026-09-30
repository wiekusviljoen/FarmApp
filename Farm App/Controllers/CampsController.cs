using Microsoft.AspNetCore.Authorization;
using Farm_App.Data;
using Farm_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Farm_App.Controllers;

[Authorize]
public class CampsController(ApplicationDbContext db) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        var camps = await db.FarmCamps.AsNoTracking()
            .Where(c => c.OwnerId == CurrentUserId)
            .OrderBy(c => c.Name).ToListAsync();
        var livestock = await db.Livestock.AsNoTracking()
            .Where(a => a.OwnerId == CurrentUserId && a.Status == "Active")
            .ToListAsync();
        ViewBag.Stock = livestock;
        ViewBag.UnassignedCount = livestock.Count(a => string.IsNullOrWhiteSpace(a.Camp));
        return View(camps);
    }

    public IActionResult Create() => View(new FarmCamp());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FarmCamp camp)
    {
        Normalize(camp);
        if (!ModelState.IsValid) return View(camp);
        camp.OwnerId = CurrentUserId;
        db.FarmCamps.Add(camp);
        await db.SaveChangesAsync();
        TempData["Message"] = "Camp created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var camp = await db.FarmCamps.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == CurrentUserId);
        return camp == null ? NotFound() : View(camp);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FarmCamp camp)
    {
        if (id != camp.Id) return NotFound();
        Normalize(camp);
        var existing = await db.FarmCamps.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == CurrentUserId);
        if (existing == null) return NotFound();
        if (!ModelState.IsValid) return View(camp);
        existing.Name = camp.Name;
        existing.Hectares = camp.Hectares;
        existing.CapacitySpecies = camp.CapacitySpecies;
        existing.CapacityMode = camp.CapacityMode;
        existing.HectaresPerHead = camp.HectaresPerHead;
        existing.ManualCapacity = camp.ManualCapacity;
        existing.Notes = camp.Notes;
        await db.SaveChangesAsync();
        TempData["Message"] = "Camp updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveAll(int id, string destinationCamp)
    {
        var source = await db.FarmCamps.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == CurrentUserId);
        if (source == null) return NotFound();
        destinationCamp = destinationCamp?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(destinationCamp) || destinationCamp.Equals(source.Name, StringComparison.OrdinalIgnoreCase))
        {
            TempData["CampError"] = "Choose a different destination camp.";
            return RedirectToAction(nameof(Index));
        }

        var destination = await db.FarmCamps.FirstOrDefaultAsync(c =>
            c.OwnerId == CurrentUserId && c.Name.ToLower() == destinationCamp.ToLower());
        if (destination == null)
        {
            TempData["CampError"] = "Destination camp was not found.";
            return RedirectToAction(nameof(Index));
        }

        var animals = await db.Livestock.Where(a =>
            a.OwnerId == CurrentUserId && a.Status == "Active" && a.Camp == source.Name).ToListAsync();
        if (animals.Count == 0)
        {
            TempData["CampError"] = $"No active animals are currently in {source.Name}.";
            return RedirectToAction(nameof(Index));
        }

        var movingForCapacity = animals.Count(a =>
            a.Species.Equals(destination.CapacitySpecies, StringComparison.OrdinalIgnoreCase));
        var destinationCurrent = await db.Livestock.CountAsync(a =>
            a.OwnerId == CurrentUserId && a.Status == "Active" &&
            a.Camp == destination.Name &&
            a.Species.ToLower() == destination.CapacitySpecies.ToLower());
        var capacity = destination.CapacityMode == "Manual"
            ? (destination.ManualCapacity ?? 0)
            : (int)Math.Floor(destination.Hectares / destination.HectaresPerHead);
        if (destinationCurrent + movingForCapacity > capacity)
        {
            TempData["CampError"] = $"{destination.Name} would be over capacity for {destination.CapacitySpecies}: {destinationCurrent} + {movingForCapacity} head, capacity {capacity}.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var animal in animals)
        {
            animal.Camp = destination.Name;
            db.LivestockEvents.Add(new LivestockEvent
            {
                OwnerId = CurrentUserId,
                LivestockId = animal.Id,
                EventType = "Movement",
                EventDate = DateTime.Today,
                Notes = $"Bulk camp move: {source.Name} → {destination.Name}"
            });
        }

        await db.SaveChangesAsync();
        TempData["Message"] = $"{animals.Count} active animals moved from {source.Name} to {destination.Name}.";
        return RedirectToAction(nameof(Index));
    }

    private static void Normalize(FarmCamp camp)
    {
        camp.Name = camp.Name?.Trim() ?? "";
        camp.CapacitySpecies = string.IsNullOrWhiteSpace(camp.CapacitySpecies) ? "Sheep" : camp.CapacitySpecies.Trim();
        camp.CapacityMode = camp.CapacityMode == "Manual" ? "Manual" : "Calculated";
        if (camp.CapacityMode == "Calculated") camp.ManualCapacity = null;
    }
}