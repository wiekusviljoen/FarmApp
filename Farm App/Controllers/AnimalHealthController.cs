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
            1319.95m, "100 doses", "Agra / Design Biologix catalogue", "2026-03 promo", true),
        new("Ovipast", "Vaccine", "Sheep, Goat", "Prevention of pneumonia / pasteurellosis",
            "1 ml", "Subcutaneous (SC)", "Give 1 ml subcutaneously, followed by a 1 ml booster 3–4 weeks later. Annual revaccination with 1 ml is listed in the 2026 Design Biologix catalogue.",
            null, "Vial", "Agra / Design Biologix catalogue", "2026 catalogue", true),
        new("Ovivax", "Vaccine", "Sheep, Goat", "Respiratory disease complex / pneumonia prevention",
            "2 ml", "Subcutaneous (SC)", "Give 2 ml subcutaneously, followed by a 2 ml booster 3–4 weeks later. Annual revaccination with 2 ml is listed in the 2026 Design Biologix catalogue.",
            null, "Vial", "Agra / Design Biologix catalogue", "2026 catalogue", true),
        new("Lumpyvax", "Vaccine", "Cattle", "Lumpy skin disease prevention",
            "1 ml", "Subcutaneous (SC)", "Use only according to the current registered product label and current veterinary programme. Agra's Namibia guidance recommends annual vaccination before peak insect season.",
            null, "Vial", "Agra / MSD Animal Health reference", "Agra 2025 guidance", false),
        new("Clostrivax B+", "Vaccine", "Sheep, Goat", "Clostridial disease prevention",
            "Check current product label", "Subcutaneous (SC)", "Agra lists Clostrivax B+ among animal-health products. Use the current registered label for species, dose and booster schedule.",
            749.95m, "50 doses", "Agra", "2026-03 promo", false),
        new("Chlamyvax", "Vaccine", "Sheep", "Chlamydial abortion prevention",
            "Check current product label", "Subcutaneous (SC)", "Use the current registered label and veterinary vaccination programme, especially around breeding.",
            969.95m, "100 ml", "Agra", "2026-03 promo", false),
        new("Riftvax", "Vaccine", "Cattle, Sheep, Goat", "Rift Valley fever prevention",
            "Check current product label", "Subcutaneous (SC)", "Use the current registered label and veterinary/state-vet programme. Agra lists Riftvax in its animal-health resources.",
            1189.95m, "100 ml", "Agra", "2026-03 promo", false),
        new("Agramycin 23% LA", "Treatment", "Livestock", "Antibiotic treatment",
            "Product-specific dose required", "Injection", "Antibiotics require a confirmed indication, species/body mass, correct product label and withdrawal period. FarmFlow will not guess an antibiotic dose.",
            529.95m, "500 ml", "Agra", "2026-03 promo", false),
        new("Dectomax", "Treatment", "Livestock", "Parasite control",
            "Product-specific dose required", "Injection", "Dose depends on species and body mass. Follow the registered product label and withdrawal period.",
            799.95m, "250 ml", "Agra", "2026-03 promo", false),
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
        var smallStock = species.Equals("Sheep", StringComparison.OrdinalIgnoreCase) || species.Equals("Goat", StringComparison.OrdinalIgnoreCase);

        if (ContainsAny(text, "snake", "adder", "mamba", "snakebite", "snake bite", "slangbyt"))
            return new("Snake bite", null, "Snake bite suspected. Keep the animal quiet, avoid unnecessary handling and seek veterinary help urgently. Do not use a vaccine as snake-bite treatment.", true);

        if (ContainsAny(text, "poison", "poisoning", "toxic", "toxin", "plant poisoning", "gif"))
            return new("Possible poisoning", null, "Possible poisoning is an emergency. Remove access to the suspected source, keep the packaging/plant sample if safe, and contact a veterinarian or state vet promptly. Do not guess an antidote.", true);

        if (ContainsAny(text, "anthrax"))
            return new("Anthrax", Products.Single(x => x.Name == "Supavax"), "Supavax is a preventive vaccine for cattle and sheep. Suspected anthrax in a live or recently dead animal needs veterinary/state-vet guidance and strict biosecurity; do not open or perform a home post-mortem.", true);

        if (ContainsAny(text, "fmd", "foot and mouth", "foot-and-mouth", "bek-en-klouseer", "blister", "vesicle", "drooling", "salivating", "mouth sore"))
            return new("Possible FMD", null, "Possible foot-and-mouth disease is a notifiable disease concern. Isolate the animal/herd, stop movements and contact the State Veterinary Service immediately. FarmFlow will not recommend a treatment vaccine for a suspected outbreak.", true);

        if (ContainsAny(text, "lumpy skin", "lumpy", "nodular skin"))
            return new("Possible lumpy skin disease", null, "Lumpy skin disease should be assessed and reported according to current veterinary rules. FarmFlow can show Lumpyvax in the catalogue, but a suspected clinical case is not treated by simply vaccinating the sick animal.", true);

        if (ContainsAny(text, "pulpy", "enterotox", "enterotoxaemia", "enterotoxemia", "overeating disease"))
            return new("Pulpy kidney / enterotoxaemia", Products.Single(x => x.Name == "Pulpyvax"), "Pulpyvax is for prevention in sheep and goats. It is not a treatment for an animal already showing severe disease; urgent cases need veterinary assessment.", true);

        if (ContainsAny(text, "blackleg", "black quarter", "blackquarter"))
            return new("Blackleg", Products.Single(x => x.Name == "Supavax"), "Supavax is registered for active immunisation against blackleg in cattle and sheep. Vaccination is preventive, not a treatment for an already sick animal.", true);

        if (ContainsAny(text, "botul", "botulism"))
        {
            var product = smallStock ? Products.Single(x => x.Name == "Botuvax") : Products.Single(x => x.Name == "Supavax");
            return new("Botulism", product, "Botulism vaccination is preventive. Weakness, inability to stand or swallowing problems need veterinary assessment.", true);
        }

        if (ContainsAny(text, "3 day", "three day", "stiff sickness", "stiffsickness", "ephemeral fever"))
            return new("3-day stiff sickness", Products.Single(x => x.Name == "BEF-Tect"), "BEF-Tect is listed for prophylactic immunisation of cattle. It is not a treatment for an animal already showing clinical signs.", true);

        if (ContainsAny(text, "blue tongue", "bluetongue"))
            return new("Bluetongue", Products.Single(x => x.Name == "Blu-Vax"), "Blu-Vax is for prophylactic immunisation of healthy sheep. Suspected disease should be assessed by a veterinarian.", true);

        if (smallStock && ContainsAny(text, "pneumonia", "pasteurella", "pasteurellosis", "lung", "cough", "coughing", "breathing", "respiratory"))
            return new("Respiratory disease / pneumonia", Products.Single(x => x.Name == "Ovipast"), "Ovipast is a preventive vaccine for sheep and goats. Coughing, laboured breathing, fever or animals going down require veterinary assessment rather than treating the vaccine as a cure.", true);

        if (smallStock && ContainsAny(text, "pneumonia", "respiratory", "lung disease"))
            return new("Respiratory disease prevention", Products.Single(x => x.Name == "Ovivax"), "Ovivax is a preventive vaccine for healthy sheep and goats. It is not a treatment for active pneumonia.", true);

        if (ContainsAny(text, "mastitis", "udder hot", "udder swollen", "udder hard", "milk abnormal", "milk fever", "hypocalcemia"))
            return new("Possible mastitis", null, "Mastitis can require rapid veterinary treatment. Check the udder and milk, separate milk where appropriate and contact a veterinarian; FarmFlow will not guess an antibiotic or withdrawal period.", true);

        if (ContainsAny(text, "birth problem", "difficult birth", "dystocia", "stuck", "lamb stuck", "kid stuck", "calf stuck", "giving birth", "labour", "labor"))
            return new("Birth / dystocia problem", null, "A difficult birth is time-sensitive. If the animal is straining without progress or a fetus is stuck, seek veterinary or experienced livestock assistance promptly rather than injecting a vaccine.", true);

        if (ContainsAny(text, "after birth", "post birth", "retained placenta", "placenta not out", "afterbirth", "weak after lambing", "weak after kidding", "newborn weak", "weak newborn", "newborn not drinking"))
            return new("Post-birth problem", null, "Weakness after birth, retained placenta, fever or foul discharge can have several causes. Keep the dam and newborn warm and seek veterinary assessment if she is weak, unable to stand, feverish or deteriorating.", true);

        if (ContainsAny(text, "diarrhea", "diarrhoea", "scours", "scour", "loose stool", "watery stool"))
            return new("Diarrhoea / scours", null, "Diarrhoea can result from parasites, infection, diet or toxins. Prevent dehydration, assess the animal and contact a veterinarian promptly for young, weak, bloody or rapidly worsening cases. FarmFlow will not guess an antibiotic or dewormer.", true);

        if (ContainsAny(text, "footrot", "foot rot", "rotten foot", "lameness", "limping", "sore foot", "hoof", "flystrike", "maggots"))
            return new("Foot / hoof problem", null, "Check the hoof and between the claws, isolate badly affected animals where practical and seek veterinary guidance for suspected footrot or severe lameness. Treatment depends on the cause.", true);

        if (ContainsAny(text, "pinkeye", "pink eye", "eye infection", "eye swollen", "cloudy eye", "eye discharge"))
            return new("Eye problem / possible pinkeye", null, "Isolate an affected animal where practical and protect the eye from dust and flies. Severe pain, a cloudy/ulcerated cornea or rapid spread needs veterinary assessment.", true);

        if (ContainsAny(text, "tick", "ticks", "teek", "teke"))
            return new("Ticks / external parasites", Products.Single(x => x.Name == "Ecomectin 1% Injectable"), "Parasite control depends on the parasite, species and body mass. Follow the registered product label; FarmFlow will not guess an injectable dose.", false);

        if (ContainsAny(text, "worm", "worms", "drench", "internal parasite", "parasite", "parasiete"))
            return new("Internal parasites", Products.Single(x => x.Name == "Ivomec Injectable"), "Confirm the parasite and product label before dosing. FarmFlow will not calculate an injectable dose without verified label information.", false);

        if (ContainsAny(text, "sudden death", "died suddenly", "found dead", "dead suddenly"))
            return new("Sudden death", null, "Do not assume the cause from the description alone. Restrict access to the carcass, avoid opening it, record what you observed and contact a veterinarian or state vet—especially if more than one animal is affected.", true);

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