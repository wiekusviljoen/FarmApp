using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Farm_App.Models;

public class LivestockEvent
{
    public int Id { get; set; }

    [ValidateNever]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    public int LivestockId { get; set; }

    [ValidateNever]
    public Livestock? Livestock { get; set; }

    [Required, StringLength(30)]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Other";

    [Required, DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime EventDate { get; set; } = DateTime.Today;

    [Range(0, 100000000)]
    [Display(Name = "Amount (NAD)")]
    public decimal? Amount { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}