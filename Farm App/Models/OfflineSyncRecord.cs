using System.ComponentModel.DataAnnotations;

namespace Farm_App.Models;

public class OfflineSyncRecord
{
    public int Id { get; set; }

    [Required, StringLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string ClientId { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string ItemType { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
