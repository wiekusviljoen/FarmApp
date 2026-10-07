using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;

namespace Farm_App.Controllers;

public class MarketController : Controller
{
    private readonly IMemoryCache _cache;

    public MarketController(IMemoryCache cache) => _cache = cache;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> FeedProducts()
    {
        const string cacheKey = "market:feed-products";
        if (_cache.TryGetValue(cacheKey, out string? cachedJson) && !string.IsNullOrEmpty(cachedJson)) return Content(cachedJson, "application/json");
        const string source = "https://www.feedmaster.com.na/products";
        var pages = new[]
        {
            (Path: "/products/sheep", Group: "Sheep"),
            (Path: "/products/beef-cattle", Group: "Cattle"),
            (Path: "/products/goats", Group: "Goats"),
            (Path: "/products/game", Group: "Game"),
            (Path: "/products/pigs", Group: "Pigs"),
            (Path: "/products/broilers", Group: "Poultry"),
            (Path: "/products/layers", Group: "Poultry"),
            (Path: "/products/dairy-cattle", Group: "Dairy"),
            (Path: "/products/horses", Group: "Horses"),
            (Path: "/products", Group: "Feedmaster products")
        };
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Farm/1.0");
            var pageResults = await Task.WhenAll(pages.Select(async page =>
            {
                try
                {
                    var pageUrl = new Uri(new Uri("https://www.feedmaster.com.na"), page.Path);
                    return (page.Group, Html: await client.GetStringAsync(pageUrl));
                }
                catch { return (page.Group, Html: ""); }
            }));

            var products = new List<object>();
            foreach (var page in pageResults)
            {
                var starts = Regex.Matches(page.Html, @"<div\b[^>]*class=[""'][^""']*product-grid-card");
                for (var i = 0; i < starts.Count; i++)
                {
                    var start = starts[i].Index;
                    var end = i + 1 < starts.Count ? starts[i + 1].Index : page.Html.Length;
                    var card = page.Html[start..end];
                    var titleMatch = Regex.Match(card, @"<h5\b[^>]*class=[""'][^""']*product-grid-title[^""']*[""'][^>]*>(.*?)</h5>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (!titleMatch.Success) continue;
                    var name = WebUtility.HtmlDecode(Regex.Replace(titleMatch.Groups[1].Value, "<[^>]+>", " ")).Trim();
                    name = Regex.Replace(name, @"T$", "").Trim();
                    var massMatch = Regex.Match(card, @"Mass:\s*([^<]+)", RegexOptions.IgnoreCase);
                    var massText = massMatch.Success ? massMatch.Groups[1].Value.Trim() : "";
                    var weightMatch = Regex.Match(massText, @"(\d+(?:[.,]\d+)?)\s*(?:kg|Kg)", RegexOptions.IgnoreCase);
                    var bagKg = weightMatch.Success && decimal.TryParse(weightMatch.Groups[1].Value.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 50m;
                    var linkMatch = Regex.Match(card, @"<a\b[^>]*href=[""']([^""']+)[""'][^>]*>", RegexOptions.IgnoreCase);
                    products.Add(new { name, group = page.Group, mass = massText, bagKg, url = linkMatch.Success ? linkMatch.Groups[1].Value : source });
                }
            }

            var unique = products.GroupBy(p => p.GetType().GetProperty("name")!.GetValue(p)!.ToString(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()).ToList();
            if (unique.Count == 0)
                return StatusCode(502, new { error = "Feedmaster's product catalog returned no recognizable products.", source });
            var payload = JsonSerializer.Serialize(new { source, fetchedAt = DateTimeOffset.Now, products = unique });
            _cache.Set(cacheKey, payload, TimeSpan.FromHours(6));
            return Content(payload, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "Could not refresh Feedmaster's product catalog: " + ex.Message, source });
        }
    }

    [HttpGet]
    public async Task<IActionResult> LiveMeat()
    {
        const string cacheKey = "market:live-meat";
        if (_cache.TryGetValue(cacheKey, out string? cachedJson) && !string.IsNullOrEmpty(cachedJson)) return Content(cachedJson, "application/json");
        const string source = "https://www.feedmaster.com.na/meat-prices";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Farm/1.0");
            var html = await client.GetStringAsync(source);
            var rows = new List<object>();

            // Keep Feedmaster's meat and auction sections distinct:
            // Beef -> Beef, Mutton -> Sheep, Game -> Wild, Auctions -> Auctions.
            var tabs = new[]
            {
                new { Id = "beef", Category = "Beef", ItemHeader = "Grade", PriceHeaders = new[] { "Meatco", "FMM", "Beefcor", "RMAA" } },
                new { Id = "mutton", Category = "Sheep", ItemHeader = "Grade", PriceHeaders = new[] { "NC Avg.", "FMM", "Aranos", "BMP", "Namibia Avg.", "RMAA" } },
                new { Id = "game", Category = "Wild", ItemHeader = "Grade", PriceHeaders = new[] { "Avg.", "Min", "Max" } },
                new { Id = "auctions", Category = "Auctions", ItemHeader = "Type", PriceHeaders = new[] { "Price" } }
            };

            string Clean(string htmlPart) =>
                WebUtility.HtmlDecode(Regex.Replace(htmlPart, "<[^>]+>", " ")).Trim();

            foreach (var tab in tabs)
            {
                // The tab pane can contain nested elements, so use the pane's start and
                // the next sibling pane as the section boundary.
                var start = Regex.Match(html, $@"<div\b(?=[^>]*\bid=[""']{Regex.Escape(tab.Id)}[""'])[^>]*>", RegexOptions.IgnoreCase);
                if (!start.Success) continue;
                var nextPane = Regex.Match(html[(start.Index + start.Length)..], @"<div\b[^>]*class=[""'][^""']*tab-pane", RegexOptions.IgnoreCase);
                var sectionEnd = nextPane.Success ? start.Index + start.Length + nextPane.Index : html.Length;
                var section = html[start.Index..sectionEnd];

                var table = Regex.Match(section, @"<table\b[^>]*>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (!table.Success) continue;

                var headerCells = Regex.Matches(table.Groups[1].Value, @"<th\b[^>]*>(.*?)</th>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                    .Cast<Match>().Select(m => Clean(m.Groups[1].Value)).ToList();
                var itemIndex = headerCells.FindIndex(h => h.Equals(tab.ItemHeader, StringComparison.OrdinalIgnoreCase));
                var dateIndex = headerCells.FindIndex(h => h.Equals("Date", StringComparison.OrdinalIgnoreCase));
                if (dateIndex < 0 || itemIndex < 0) continue;

                var priceColumns = tab.PriceHeaders
                    .Select(name => new { Name = name, Index = headerCells.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase)) })
                    .Where(x => x.Index >= 0)
                    .ToList();

                foreach (Match row in Regex.Matches(table.Groups[1].Value, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                {
                    var cells = Regex.Matches(row.Groups[1].Value, @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                        .Cast<Match>().Select(m => Clean(m.Groups[1].Value)).ToList();
                    if (cells.Count <= Math.Max(dateIndex, itemIndex)) continue;
                    if (!DateTime.TryParseExact(cells[dateIndex], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;

                    var item = cells[itemIndex];
                    foreach (var column in priceColumns)
                    {
                        if (column.Index >= cells.Count) continue;
                        var priceText = cells[column.Index].Replace(",", "");
                        if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0) continue;

                        rows.Add(new
                        {
                            date = date.ToString("yyyy-MM-dd"),
                            category = tab.Category,
                            item,
                            sourceName = tab.Category == "Auctions" ? "Feedmaster Auctions" : column.Name,
                            price
                        });
                    }
                }
            }

            if (rows.Count == 0)
                return StatusCode(502, new { error = "Feedmaster page returned no recognizable beef, mutton, or game price rows.", source });

            var payload = JsonSerializer.Serialize(new { source, fetchedAt = DateTimeOffset.Now, prices = rows });
            _cache.Set(cacheKey, payload, TimeSpan.FromMinutes(15));
            return Content(payload, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "Could not retrieve live meat prices: " + ex.Message, source });
        }
    }
}
