using System.Net;
using System.Text.RegularExpressions;
using Farm_App.Models;

namespace Farm_App.Services;

public sealed class AuctionFeedService : BackgroundService
{
    private const string AgraCommercialUrl = "https://www.agra.com.na/index.php/commercial-auctions/auctions_filter";
    private const string AgraWeanerUrl = "https://www.agra.com.na/index.php/weaner-auctions/auctions_filter";
    private const string AgraStudUrl = "https://www.agra.com.na/index.php/stud-auctions/auctions_filter";
    private const string WhklaUrl = "https://www.whkla.com/auction-calendar/";
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<AuctionFeedService> _logger;
    private readonly object _sync = new();
    private List<AuctionEvent> _events = new();
    private string? _notice;
    private DateTime _lastUpdated;
    public AuctionFeedService(IHttpClientFactory clients, ILogger<AuctionFeedService> logger) { _clients = clients; _logger = logger; }
    public List<AuctionEvent> GetEvents() { lock (_sync) return _events.ToList(); }
    public string? GetNotice() { lock (_sync) return _notice; }
    public DateTime? GetLastUpdated() { lock (_sync) return _lastUpdated == default ? null : _lastUpdated; }

    public async Task RefreshAsync(CancellationToken token = default)
    {
        var client = _clients.CreateClient("AuctionFeed");
        var events = new List<AuctionEvent>(); string? notice = null;
        foreach (var source in new[] { (Url: AgraCommercialUrl, Kind: "commercial"), (Url: AgraWeanerUrl, Kind: "weaner"), (Url: AgraStudUrl, Kind: "stud") })
        {
            try { events.AddRange(ParseAgra(await client.GetStringAsync(source.Url, token), source.Kind, source.Url)); }
            catch (Exception ex) { _logger.LogWarning(ex, "Agra {AuctionKind} feed refresh failed", source.Kind); }
        }
        try
        {
            var html = await client.GetStringAsync(WhklaUrl, token);
            var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));
            if (Regex.IsMatch(text, "all scheduled auctions are cancelled|all auctions cancelled", RegexOptions.IgnoreCase))
                notice = "Windhoek Livestock Auctioneers reports scheduled auctions cancelled until further notice. Check the official notice before attending.";
            events.AddRange(ParseWhkla(html));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "WHKLA auction feed refresh failed"); }
        lock (_sync)
        {
            if (events.Count > 0) _events = events.GroupBy(x => (x.Name.ToLowerInvariant(), x.Date.Date, x.Venue?.ToLowerInvariant())).Select(g => g.First()).ToList();
            _notice = notice; _lastUpdated = DateTime.Now;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RefreshAsync(stoppingToken);
    }

    private static List<AuctionEvent> ParseAgra(string html, string sourceKind, string sourceUrl)
    {
        var results = new List<AuctionEvent>();
        var blocks = Regex.Matches(html, @"(?is)<div\s+class=""auction_listing"">(.*?)(?=<div\s+id=""aside""|$)");
        foreach (Match block in blocks)
        {
            var chunk = block.Groups[1].Value;
            string Field(string className)
            {
                var match = Regex.Match(chunk, $@"(?is)<div\s+class=""{Regex.Escape(className)}"">(.*?)</div>");
                return match.Success ? WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "<[^>]+>", " ")).Trim() : "";
            }
            var name = Regex.Replace(Field("auction_title"), @"\s+", " ").Trim();
            var dateText = Field("auction_date");
            var town = Regex.Replace(Field("auction_town"), @"^Town:\s*", "", RegexOptions.IgnoreCase).Trim();
            var dateMatch = Regex.Match(dateText, @"(?<date>\d{1,2}\s+[A-Za-z]+\s+\d{4})\s+at\s+(?<time>\d{1,2}:\d{2})", RegexOptions.IgnoreCase);
            if (name.Length < 4 || !dateMatch.Success || !DateTime.TryParse(dateMatch.Groups["date"].Value, out var date)) continue;
            var animal = ClassifyLivestock(name, sourceKind);
            results.Add(new AuctionEvent { Id = Guid.NewGuid(), IsImported = true, Name = CultureTitle(name), Date = date, Time = dateMatch.Groups["time"].Value, Organizer = "Agra Namibia", Venue = string.IsNullOrWhiteSpace(town) ? "See official listing" : town, ProvinceOrRegion = town, AuctionType = sourceKind == "stud" ? "Stud / breeding" : "Commercial livestock", Livestock = animal, Website = sourceUrl, Details = $"Imported from Agra's {sourceKind} auction listing. Livestock category: {animal}. Confirm details with the organizer." });
        }
        return results;
    }

    private static List<AuctionEvent> ParseWhkla(string html)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));
        var results = new List<AuctionEvent>();
        var pattern = @"(?<date>\d{1,2}\s+[A-Za-z]+\s+\d{4})\s+(?<time>\d{1,2}:\d{2})\s+(?<town>[A-Za-z -]{3,40})\s+(?<kind>Large Stock|Small Stock|Weaner)";
        foreach (Match m in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
            if (DateTime.TryParse(m.Groups["date"].Value, out var date)) results.Add(new AuctionEvent { Id = Guid.NewGuid(), IsImported = true, Name = m.Groups["town"].Value.Trim() + " " + m.Groups["kind"].Value + " Auction", Date = date, Time = m.Groups["time"].Value, Organizer = "Windhoek Livestock Auctioneers", Venue = m.Groups["town"].Value.Trim(), ProvinceOrRegion = m.Groups["town"].Value.Trim(), AuctionType = "Livestock", Livestock = m.Groups["kind"].Value.Equals("Small Stock", StringComparison.OrdinalIgnoreCase) ? "Sheep / goats" : m.Groups["kind"].Value.Equals("Weaner", StringComparison.OrdinalIgnoreCase) ? "Weaner cattle" : "Cattle", Website = WhklaUrl, Details = "Imported automatically. Check the official notice for cancellations or changes." });
        return results;
    }
    private static string ClassifyLivestock(string name, string sourceKind = "")
    {
        var n = name.ToUpperInvariant();
        if (n.Contains("WEANER")) return "Weaner cattle";
        if (n.Contains("SMALL STOCK") || n.Contains("SMALL COMMERCIAL") || n.Contains("SHEEP") || n.Contains("LAMB") || n.Contains("DORPER")) return "Sheep / small stock";
        if (n.Contains("GOAT")) return "Goats";
        if (n.Contains("LARGE STOCK") || n.Contains("CATTLE") || n.Contains("BEEFMASTER") || n.Contains("BONSMARA") || n.Contains("BORAN") || n.Contains("BRAHMAN")) return "Cattle";
        if (n.Contains("PIG")) return "Pigs";
        if (n.Contains("GAME") || n.Contains("WILD")) return "Game";
        if (sourceKind == "weaner") return "Weaner cattle";
        if (sourceKind == "stud") return "Stud breeding stock (animal not specified)";
        return "Livestock type not specified by Agra";
    }
    private static string CultureTitle(string value) => string.Join(' ', value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
