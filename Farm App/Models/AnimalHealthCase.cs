using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Farm_App.Models;

public class AnimalHealthCase
{
    public int Id { get; set; }

    [ValidateNever]
    public string OwnerId { get; set; } = string.Empty;

    public int? LivestockId { get; set; }

    [StringLength(30)]
    [Display(Name = "Animal tag / ID")]
    public string? AnimalTag { get; set; }

    [Required, StringLength(30)]
    public string Species { get; set; } = "Sheep";

    [Required, StringLength(1200)]
    [Display(Name = "What happened / what is wrong?")]
    public string ProblemDescription { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Known cause / suspected cause")]
    public string? SuspectedCause { get; set; }

    [StringLength(60)]
    public string? Category { get; set; }

    [StringLength(120)]
    public string? Recommendation { get; set; }

    public bool VetAttentionRecommended { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}