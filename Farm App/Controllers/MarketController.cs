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
            foreach (Match row in Regex.Matches(html, "<tr\\b[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var cells = Regex.Matches(row.Groups[1].Value, "<td\\b[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (cells.Count < 3) continue;
                string Clean(Match m) => WebUtility.HtmlDecode(Regex.Replace(m.Groups[1].Value, "<[^>]+>", " ")).Trim();
                var dateText = Clean(cells[0]);
                var category = Clean(cells[1]);
                var priceText = Clean(cells[2]).Replace(",", "");
                if (!DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
                if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)) continue;
                rows.Add(new { date = date.ToString("yyyy-MM-dd"), category, price });
            }
            if (rows.Count == 0) return StatusCode(502, new { error = "Feedmaster page returned no recognizable price rows.", source });
            return Json(new { source, fetchedAt = DateTimeOffset.Now, prices = rows });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "Could not retrieve live meat prices: " + ex.Message, source });
        }
    }
}
