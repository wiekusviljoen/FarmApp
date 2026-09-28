using Farm_App.Data;
using Farm_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Controllers;

public class LivestockController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? q)
    {
        var animals = db.Livestock.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            animals = animals.Where(a => a.TagNumber.Contains(q) || a.Species.Contains(q) || (a.Camp != null && a.Camp.Contains(q)));
        ViewBag.Query = q;
        return View(await animals.OrderBy(a => a.Species).ThenBy(a => a.TagNumber).ToListAsync());
    }

    public IActionResult Create() => View(new Livestock());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Livestock animal)
    {
        if (!ModelState.IsValid) return View(animal);
        db.Add(animal);
        await db.SaveChangesAsync();
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
        db.Update(animal);
        await db.SaveChangesAsync();
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
