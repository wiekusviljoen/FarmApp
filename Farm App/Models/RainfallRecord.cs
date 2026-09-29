using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Farm_App.Models;

public class RainfallRecord
{
    public int Id { get; set; }

    [ValidateNever]
    public string OwnerId { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [StringLength(60)]
    public string? Camp { get; set; }

    [Range(0, 2000)]
    [Display(Name = "Rainfall (mm)")]
    public decimal Millimeters { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
