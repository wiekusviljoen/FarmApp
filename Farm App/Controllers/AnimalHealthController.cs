using System.Security.Claims;
using Farm_App.Data;
using Farm_App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farm_App.Controllers;

[Authorize]
public class AnimalHealthController(ApplicationDbContext db) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private static readonly List<HealthProduct> Products =
    [
        new("Pulpyvax", "Vaccine", "Sheep, Goat", "Pulpy kidney disease (enterotoxaemia)",
            "1 ml", "Subcutaneous (SC)", "Shake well. Lambs/kids can be immunised from 3 months. Give a second dose 4–6 weeks later. In goats, inject in the tail fold.",
            99.95m, "100 ml", "Agra", "2026-01", true),
        new("Supavax", "Vaccine", "Cattle, Sheep", "Anthrax, botulism and blackleg",
            "2 ml", "Subcutaneous (SC)", "For cattle, first vaccination is followed by DUOVAX 4–6 weeks later. Adult animals not previously immunised are boosted 4–6 weeks later. Annual SUPAVAX booster thereafter.",
            439.95m, "50 ml", "Agra", "2026-01", true),
        new("Botuvax", "Vaccine", "Cattle, Horse, Sheep, Goat", "Botulism",
            "Cattle/horses: 2 ml; sheep/goats: 1 ml", "Subcutaneous (SC)", "Animals can be vaccinated from 3 months. Previously unvaccinated animals receive 2 injections 4–6 weeks apart, then an annual booster. Goats: tail fold.",
            null, "100 ml", "Agra", "Label information", true),
        new("BEF-Tect", "Vaccine", "Cattle", "Ephemeral fever / 3-day stiff sickness",
            "2 ml", "Subcutaneous (SC)", "Initial dose followed by a 2 ml booster 3–4 weeks later. Annual revaccination is listed in the manufacturer catalogue.",
            null, "10 doses", "Agra / Design Biologix catalogue", "2026 catalogue", true),
        new("Blu-Vax", "Vaccine", "Sheep", "Bluetongue disease",
            "1 ml", "Subcutaneous (SC)", "Vaccination is completed before the outbreak season. A second 1 ml dose is given 3–4 weeks later; annual revaccination is listed in the manufacturer catalogue.",
            null, "Vial", "Agra / Design Biologix catalogue", "2026 catalogue", true),
        new("Wound-Sept Aerosol", "Treatment", "Livestock", "Topical wound care / wound antisepsis",
            "Use only according to the product label", "Topical", "Clean and assess the wound first. Follow the product label. Deep, punctured, infected or heavily bleeding wounds should be assessed by a veterinarian.",
            169.95m, "350 ml", "Agra", "2026-01", false),
        new("Ecomectin 1% Injectable", "Treatment", "Cattle, Sheep, Pig", "Parasite control",
            "Product-specific dose required", "Injection", "Dose depends on species, body mass and the registered product label. FarmFlow will not calculate this dose without verified label instructions.",
            79.95m, "20 ml", "Agra", "2026-01", false),
        new("Ivomec Injectable", "Treatment", "Cattle, Sheep, Pig", "Parasite control",
            "Product-specific dose required", "Injection", "Dose depends on species and body mass. Follow the current product label or veterinary direction.",
            414.95m, "200 ml", "Agra", "2026-01", false)
    ];

    public async Task<IActionResult> Index()
    {
        var cases = await db.AnimalHealthCases.AsNoTracking()
            .Where(x => x.OwnerId == CurrentUserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(30)
            .ToListAsync();

        ViewBag.Products = Products;
        ViewBag.Animals = await db.Livestock.AsNoTracking()
            .Where(x => x.OwnerId == CurrentUserId && x.Status == "Active")
            .OrderBy(x => x.Species).ThenBy(x => x.TagNumber)
            .ToListAsync();

        if (TempData["HealthProduct"] is string productName)
        {
            ViewBag.HealthCategory = TempData["HealthCategory"];
            ViewBag.HealthRecommendation = TempData["HealthRecommendation"];
            ViewBag.HealthVet = TempData["HealthVet"];
            ViewBag.HealthProduct = Products.FirstOrDefault(x => x.Name == productName);
        }

        return View(cases);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Recommend(string species, string? animalTag, int? livestockId,
        string problemDescription, string? suspectedCause)
    {
        species = string.IsNullOrWhiteSpace(species) ? "Other" : species.Trim();
        problemDescription = (problemDescription ?? string.Empty).Trim();
        suspectedCause = string.IsNullOrWhiteSpace(suspectedCause) ? null : suspectedCause.Trim();

        if (problemDescription.Length < 3)
        {
            TempData["HealthError"] = "Please describe what happened or what is wrong with the animal.";
            return RedirectToAction(nameof(Index));
        }

        var result = FindRecommendation(species, problemDescription, suspectedCause);
        var selectedAnimal = livestockId.HasValue
            ? await db.Livestock.AsNoTracking().FirstOrDefaultAsync(x => x.Id == livestockId && x.OwnerId == CurrentUserId)
            : null;

        var healthCase = new AnimalHealthCase
        {
            OwnerId = CurrentUserId,
            LivestockId = selectedAnimal?.Id,
            AnimalTag = selectedAnimal?.TagNumber ?? animalTag,
            Species = selectedAnimal?.Species ?? species,
            ProblemDescription = problemDescription,
            SuspectedCause = suspectedCause,
            Category = result.Category,
            Recommendation = result.Product?.Name ?? result.Recommendation,
            VetAttentionRecommended = result.VetAttention,
            Notes = result.Product == null ? result.Recommendation : result.Product.Indication
        };

        db.AnimalHealthCases.Add(healthCase);
        await db.SaveChangesAsync();

        TempData["HealthCategory"] = result.Category;
        TempData["HealthProduct"] = result.Product?.Name ?? string.Empty;
        TempData["HealthRecommendation"] = result.Recommendation;
        TempData["HealthVet"] = result.VetAttention ? "Veterinary attention recommended" : "No urgent veterinary flag from this entry";
        return RedirectToAction(nameof(Index));
    }

    private static RecommendationResult FindRecommendation(string species, string problem, string? cause)
    {
        var text = $"{problem} {cause}".ToLowerInvariant();

        if (ContainsAny(text, "snake", "adder", "mamba", "snakebite", "slangbyt"))
            return new("Snake bite", null, "Snake bite suspected: keep the animal quiet and seek veterinary help urgently. Do not use a vaccine as a snake-bite treatment.", true);

        if (ContainsAny(text, "bleed heavily", "massive bleeding", "deep wound", "puncture", "open wound", "wound"))
            return new("Wound", Products.Single(x => x.Name == "Wound-Sept Aerosol"), "Wound care product listed for topical use; the injury still needs assessment.", ContainsAny(text, "heavy", "deep", "puncture"));

        if (ContainsAny(text, "pulpy", "enterotox", "sudden death", "overeating disease"))
            return new("Pulpy kidney / enterotoxaemia", Products.Single(x => x.Name == "Pulpyvax"), "The description matches a condition Pulpyvax is registered to prevent. Vaccines are preventive, not a treatment for an already severely ill animal.", false);

        if (ContainsAny(text, "anthrax"))
            return new("Anthrax", Products.Single(x => x.Name == "Supavax"), "Anthrax is a serious veterinary/public-health concern. The vaccine is preventive; suspected clinical cases require veterinary/state-vet guidance.", true);

        if (ContainsAny(text, "blackleg", "black quarter", "blackquarter"))
            return new("Blackleg", Products.Single(x => x.Name == "Supavax"), "Supavax is registered for active immunisation against blackleg. A sick animal needs veterinary assessment.", true);

        if (ContainsAny(text, "botul", "botulism"))
        {
            var product = species.Equals("Sheep", StringComparison.OrdinalIgnoreCase) || species.Equals("Goat", StringComparison.OrdinalIgnoreCase)
                ? Products.Single(x => x.Name == "Botuvax")
                : Products.Single(x => x.Name == "Supavax");
            return new("Botulism", product, "Botulism vaccination is preventive. Animals showing neurologic weakness should be assessed by a veterinarian.", true);
        }

        if (ContainsAny(text, "3 day", "three day", "stiff sickness", "stiffsickness", "ephemeral fever"))
            return new("3-day stiff sickness", Products.Single(x => x.Name == "BEF-Tect"), "This product is listed for prophylactic immunisation of cattle. It is not a treatment for an already sick animal.", false);

        if (ContainsAny(text, "blue tongue", "bluetongue"))
            return new("Bluetongue", Products.Single(x => x.Name == "Blu-Vax"), "Blu-Vax is listed for prophylactic immunisation of healthy sheep. Suspected disease should be assessed by a veterinarian.", true);

        if (ContainsAny(text, "tick", "ticks", "teek", "teke"))
            return new("Ticks / external parasites", Products.Single(x => x.Name == "Ecomectin 1% Injectable"), "Parasite control depends on the parasite, species and body mass. Follow the registered product label; FarmFlow will not guess an injectable dose.", false);

        if (ContainsAny(text, "worm", "worms", "drench", "parasite", "parasiete"))
            return new("Internal parasites", Products.Single(x => x.Name == "Ivomec Injectable"), "Confirm the parasite and product label before dosing. FarmFlow will not calculate an injectable dose without verified label information.", false);

        return new("Unclear / needs assessment", null, "No specific product was matched safely. Record the signs, isolate an obviously sick animal where appropriate, and contact a veterinarian or state vet if the animal is deteriorating.", true);
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(text.Contains);

    public sealed record HealthProduct(
        string Name, string Type, string Species, string Indication, string Dose,
        string Route, string Directions, decimal? PriceNad, string PackSize,
        string Source, string PriceOrLabelDate, bool DoseVerified);

    public sealed record RecommendationResult(
        string Category, HealthProduct? Product, string Recommendation, bool VetAttention);
}