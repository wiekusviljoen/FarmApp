using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Farm_App.Models;

public class FarmCamp
{
    public int Id { get; set; }

    [ValidateNever]
    public string OwnerId { get; set; } = string.Empty;

    [Required, StringLength(60)]
    [Display(Name = "Camp name")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 1000000)]
    [Display(Name = "Size (hectares)")]
    public decimal Hectares { get; set; }

    [Required, StringLength(30)]
    [Display(Name = "Capacity species")]
    public string CapacitySpecies { get; set; } = "Sheep";

    [Required, StringLength(20)]
    [Display(Name = "Capacity method")]
    public string CapacityMode { get; set; } = "Calculated";

    [Range(0.01, 100000)]
    [Display(Name = "Hectares per head")]
    public decimal HectaresPerHead { get; set; } = 5m;

    [Range(0, 100000000)]
    [Display(Name = "Manual capacity (head)")]
    public int? ManualCapacity { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}