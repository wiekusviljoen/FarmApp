using System.ComponentModel.DataAnnotations;

namespace Farm_App.Models;

public class FarmPushSubscription
{
    public int Id { get; set; }
    [Required, StringLength(450)] public string OwnerId { get; set; } = string.Empty;
    [Required, StringLength(2048)] public string Endpoint { get; set; } = string.Empty;
    [Required, StringLength(512)] public string P256dh { get; set; } = string.Empty;
    [Required, StringLength(512)] public string Auth { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    [StringLength(180)] public string? LastThunderAlertKey { get; set; }
}
