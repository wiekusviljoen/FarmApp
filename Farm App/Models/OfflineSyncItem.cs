using System.Globalization;
using System.Text.Json;

namespace Farm_App.Models;

public class OfflineSyncItem
{
    public string ClientId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> Data { get; set; } = new();

    public string GetString(string key) => Data.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? string.Empty : throw new InvalidOperationException($"Missing {key}.");

    public string? GetStringOrNull(string key) => Data.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public DateTime GetDate(string key) => DateTime.Parse(GetString(key), CultureInfo.InvariantCulture);

    public DateTime? GetDateNullable(string key) => string.IsNullOrWhiteSpace(GetStringOrNull(key)) ? null : DateTime.Parse(GetString(key), CultureInfo.InvariantCulture);

    public decimal? GetDecimal(string key) => Data.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var result) ? result : null;
}
