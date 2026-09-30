using System.Security.Claims;
using Farm_App.Data;
using Farm_App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Controllers;

[Authorize]
[IgnoreAntiforgeryToken]
public class OfflineController(ApplicationDbContext db) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost]
    public async Task<IActionResult> Sync([FromBody] OfflineSyncRequest request)
    {
        if (request.Items is null || request.Items.Count == 0) return Ok(new { synced = 0, failed = 0, results = Array.Empty<object>() });
        var results = new List<object>();
        foreach (var item in request.Items.Take(100))
        {
            if (string.IsNullOrWhiteSpace(item.ClientId) || string.IsNullOrWhiteSpace(item.Type)) { results.Add(new { item.ClientId, ok = false, error = "Missing item identity." }); continue; }
            if (await db.Set<OfflineSyncRecord>().AnyAsync(x => x.OwnerId == CurrentUserId && x.ClientId == item.ClientId)) { results.Add(new { item.ClientId, ok = true, duplicate = true }); continue; }
            try
            {
                switch (item.Type)
                {
                    case "livestock-event": await SyncEvent(item); break;
                    case "create-animal": await SyncAnimal(item); break;
                    case "rainfall": await SyncRainfall(item); break;
                    default: throw new InvalidOperationException("Unknown offline item type.");
                }
                db.Add(new OfflineSyncRecord { OwnerId = CurrentUserId, ClientId = item.ClientId, ItemType = item.Type });
                await db.SaveChangesAsync();
                results.Add(new { item.ClientId, ok = true });
            }
            catch (Exception ex) { results.Add(new { item.ClientId, ok = false, error = ex.Message }); }
        }
        return Ok(new { synced = results.Count(x => (bool)x.GetType().GetProperty("ok")!.GetValue(x)!), failed = results.Count(x => !(bool)x.GetType().GetProperty("ok")!.GetValue(x)!), results });
    }

    private async Task SyncEvent(OfflineSyncItem item)
    {
        var tag = item.GetString("tagNumber");
        var animal = await db.Livestock.FirstOrDefaultAsync(x => x.OwnerId == CurrentUserId && x.TagNumber == tag) ?? throw new InvalidOperationException($"Animal '{tag}' was not found. Sync the animal first.");
        db.LivestockEvents.Add(new LivestockEvent { OwnerId = CurrentUserId, LivestockId = animal.Id, EventType = item.GetString("eventType"), EventDate = item.GetDate("eventDate"), Amount = item.GetDecimal("amount"), Notes = item.GetStringOrNull("notes") });
    }

    private async Task SyncAnimal(OfflineSyncItem item)
    {
        var tag = item.GetString("tagNumber");
        if (await db.Livestock.AnyAsync(x => x.OwnerId == CurrentUserId && x.TagNumber == tag)) throw new InvalidOperationException($"Animal '{tag}' already exists.");
        db.Livestock.Add(new Livestock { OwnerId = CurrentUserId, TagNumber = tag, Species = item.GetString("species"), Breed = item.GetStringOrNull("breed"), Sex = item.GetStringOrNull("sex"), DateOfBirth = item.GetDateNullable("dateOfBirth"), Camp = item.GetStringOrNull("camp"), Status = item.GetString("status"), PurchasePrice = item.GetDecimal("purchasePrice"), Notes = item.GetStringOrNull("notes") });
    }

    private async Task SyncRainfall(OfflineSyncItem item)
    {
        db.RainfallRecords.Add(new RainfallRecord { OwnerId = CurrentUserId, Date = item.GetDate("date"), Camp = item.GetStringOrNull("camp"), Millimeters = item.GetDecimal("millimeters") ?? 0, Notes = item.GetStringOrNull("notes") });
        await Task.CompletedTask;
    }
}

public record OfflineSyncRequest(List<OfflineSyncItem> Items);
