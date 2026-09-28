using System.ComponentModel.DataAnnotations;

namespace Farm_App.Models;

public class RainfallRecord
{
    public int Id { get; set; }

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
