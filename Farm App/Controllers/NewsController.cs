using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Farm_App.Controllers;

public class NewsController : Controller
{
    private static readonly (string Name, string Url)[] Feeds =
    {
        ("Namibian Sun", "https://www.namibiansun.com/rssFeed/113"),
        ("The Namibian Farmer", "https://namibianfarming.com/feed/"),
        ("The Brief", "https://thebrief.com.na/feed/"),
        ("The Namibian", "https://www.namibian.com.na/feed/")
    };

    private static readonly string[] AgricultureTerms =
    {
        "agricultur", "farm", "farmer", "livestock", "cattle", "beef", "sheep", "goat", "lamb", "wool", "meatco", "abattoir", "auction",
        "foot-and-mouth", "foot and mouth", "fmd", "animal health", "veterinary", "poultry", "pig", "dairy", "feed price", "animal feed", "fodder",
        "maize", "corn", "wheat", "grain", "crop", "harvest", "horticultur", "irrigat", "fertili", "seed", "drought", "rainfall", "veld", "grazing",
        "locust", "pest", "fisher", "aquacultur", "food security", "agri", "horticulture", "livestock products", "rural development"
    };

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(18) };
    private static readonly object CacheLock = new();
    private static List<NewsArticle> _cache = new();
    private static DateTimeOffset _cacheAt = DateTimeOffset.MinValue;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Feed(bool refresh = false)
    {
        lock (CacheLock)
        {
            if (!refresh && _cache.Count > 0 && DateTimeOffset.UtcNow - _cacheAt < TimeSpan.FromMinutes(10))
                return Json(new { fetchedAt = _cacheAt, articles = _cache, sources = Feeds.Length });
        }

        var results = await Task.WhenAll(Feeds.Select(FetchFeed));
        var articles = results.SelectMany(x => x)
            .Where(IsAgricultureRelated)
            .GroupBy(x => NormalizeUrl(x.Url), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
            .Take(60).ToList();

        lock (CacheLock) { _cache = articles; _cacheAt = DateTimeOffset.UtcNow; }
        if (articles.Count == 0)
            return StatusCode(502, new { error = "No agriculture stories could be loaded. Please try again shortly." });
        return Json(new { fetchedAt = _cacheAt, articles, sources = Feeds.Length });
    }

    private static async Task<List<NewsArticle>> FetchFeed((string Name, string Url) feed)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, feed.Url);
            request.Headers.UserAgent.ParseAdd("FarmFlow/1.0 (agriculture news reader)");
            using var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null, Async = false });
            var doc = XDocument.Load(reader);
            var nodes = doc.Descendants().Where(e => e.Name.LocalName is "item" or "entry");
            var articles = new List<NewsArticle>();
            foreach (var item in nodes)
            {
                var title = Clean(Text(item, "title"));
                var url = Text(item, "link");
                if (string.IsNullOrWhiteSpace(url))
                    url = item.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value ?? "";
                if (string.IsNullOrWhiteSpace(title) || !Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https")) continue;
                var description = Clean(Text(item, "description", "summary", "content", "encoded"));
                var dateText = Text(item, "pubDate", "published", "updated", "date");
                DateTimeOffset? date = DateTimeOffset.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate) ? parsedDate : null;
                var category = string.Join(", ", item.Elements().Where(e => e.Name.LocalName == "category").Select(e => Clean(e.Value)).Where(x => x.Length > 0).Distinct().Take(4));
                articles.Add(new NewsArticle(title, parsed.ToString(), date, description, feed.Name, category, AssessImpact(title, description, category)));
            }
            return articles;
        }
        catch { return new List<NewsArticle>(); }
    }

    private static bool IsAgricultureRelated(NewsArticle article)
    {
        var text = $"{article.Title} {article.Summary} {article.Category}".ToLowerInvariant();
        return AgricultureTerms.Any(term => text.Contains(term, StringComparison.Ordinal));
    }

    private static string AssessImpact(string title, string summary, string category)
    {
        var text = $"{title} {summary} {category}".ToLowerInvariant();
        if (new[] { "foot-and-mouth", "foot and mouth", "fmd", "disease outbreak", "quarantine", "movement ban", "livestock restriction" }.Any(x => text.Contains(x))) return "Animal health & movement: could restrict livestock transport, sales, and market access; check official veterinary notices before moving animals.";
        if (new[] { "drought", "dry spell", "water shortage", "low rainfall", "heatwave" }.Any(x => text.Contains(x))) return "Weather & grazing: may reduce veld growth and water availability, increasing pressure on feed budgets and herd condition.";
        if (new[] { "rainfall", "good rains", "flood", "storm", "rain forecast" }.Any(x => text.Contains(x))) return "Weather & production: could affect grazing, water supplies, planting, and access roads; local conditions determine the net effect.";
        if (new[] { "meat price", "beef price", "sheep price", "livestock price", "auction", "abattoir", "export market", "meatco" }.Any(x => text.Contains(x))) return "Livestock markets: may influence selling prices, buyer demand, and timing or route of sales; compare with local prices.";
        if (new[] { "feed price", "animal feed", "fertiliser", "fuel price", "input cost" }.Any(x => text.Contains(x))) return "Farm costs: may change feed or input expenses and affect margins; check supplier prices and cost per animal.";
        if (new[] { "export", "trade agreement", "market access", "tariff", "import" }.Any(x => text.Contains(x))) return "Trade & demand: may open or limit sales channels and affect demand or prices, depending on products and markets.";
        if (new[] { "subsidy", "grant", "funding", "loan", "support scheme" }.Any(x => text.Contains(x))) return "Finance & investment: may affect access to funding for eligible producers; verify requirements, deadlines, and terms.";
        if (new[] { "crop", "maize", "wheat", "grain", "horticulture", "harvest", "irrigation" }.Any(x => text.Contains(x))) return "Crop production: may affect yields, input needs, irrigation, or supply, with knock-on effects for food prices and livestock feed.";
        return "Industry outlook: may affect costs, production, demand, or policy. This is a general estimate; check the full report and local conditions.";
    }

    private static string Text(XElement item, params string[] names) => item.Elements().FirstOrDefault(e => names.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase))?.Value ?? "";
    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var decoded = WebUtility.HtmlDecode(value);
        var plain = Regex.Replace(decoded, "<[^>]+>", " ");
        return Regex.Replace(plain, @"\s+", " ").Trim();
    }
    private static string NormalizeUrl(string url) => url.TrimEnd('/');

    public record NewsArticle(string Title, string Url, DateTimeOffset? PublishedAt, string Summary, string Source, string Category, string Impact);
}
