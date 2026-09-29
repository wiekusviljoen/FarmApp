using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Farm_App.Models;

public class Livestock
{
    public int Id { get; set; }

    [ValidateNever]
    public string OwnerId { get; set; } = string.Empty;

    [Required, StringLength(30)]
    [Display(Name = "Tag / ID")]
    public string TagNumber { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Species { get; set; } = "Sheep";

    [StringLength(30)]
    public string? Breed { get; set; }

    [StringLength(20)]
    public string? Sex { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(60)]
    public string? Camp { get; set; }

    [Required, StringLength(25)]
    public string Status { get; set; } = "Active";

    [Range(0, 100000000)]
    [DataType(DataType.Currency)]
    [Display(Name = "Purchase price (NAD)")]
    public decimal? PurchasePrice { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
