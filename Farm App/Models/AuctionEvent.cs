using System.ComponentModel.DataAnnotations;

namespace Farm_App.Models;

public class AuctionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsImported { get; set; }
    [Required, StringLength(140)] public string Name { get; set; } = "";
    [Required] public DateTime Date { get; set; } = DateTime.Today;
    [StringLength(100)] public string? Time { get; set; }
    [StringLength(120)] public string? Organizer { get; set; }
    [StringLength(160)] public string? Venue { get; set; }
    [StringLength(80)] public string? ProvinceOrRegion { get; set; }
    [StringLength(80)] public string? AuctionType { get; set; }
    [StringLength(80)] public string? Livestock { get; set; }
    [StringLength(80)] public string? Contact { get; set; }
    [Url, StringLength(500)] public string? Website { get; set; }
    [StringLength(2000)] public string? Details { get; set; }
}
