using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace Farm_App.Controllers;

public class MarketController : Controller
{
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> LiveMeat()
    {
        const string source = "https://www.feedmaster.com.na/meat-prices";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FarmFlow/1.0");
            var html = await client.GetStringAsync(source);
            var rows = new List<object>();

            // Feedmaster publishes separate tabs. Map its terminology to farm-friendly groups:
            // Beef -> Beef, Mutton -> Sheep, Game -> Wild. Auctions are deliberately excluded.
            var tabs = new[]
            {
                new { Id = "beef", Category = "Beef", ItemHeader = "Grade", PriceHeaders = new[] { "Meatco", "FMM", "Beefcor", "RMAA" } },
                new { Id = "mutton", Category = "Sheep", ItemHeader = "Grade", PriceHeaders = new[] { "NC Avg.", "FMM", "Aranos", "BMP", "Namibia Avg.", "RMAA" } },
                new { Id = "game", Category = "Wild", ItemHeader = "Grade", PriceHeaders = new[] { "Avg.", "Min", "Max" } }
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
                            sourceName = column.Name,
                            price
                        });
                    }
                }
            }

            if (rows.Count == 0)
                return StatusCode(502, new { error = "Feedmaster page returned no recognizable beef, mutton, or game price rows.", source });

            return Json(new { source, fetchedAt = DateTimeOffset.Now, prices = rows });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "Could not retrieve live meat prices: " + ex.Message, source });
        }
    }
}
