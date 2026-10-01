using System.ComponentModel.DataAnnotations;

namespace Farm_App.Models;

public class PushServerSettings
{
    [Key] public int Id { get; set; }
    [Required, StringLength(512)] public string PublicKey { get; set; } = string.Empty;
    [Required, StringLength(512)] public string PrivateKey { get; set; } = string.Empty;
    [Required, StringLength(512)] public string Subject { get; set; } = string.Empty;
    public string? AlertStateJson { get; set; }
}
