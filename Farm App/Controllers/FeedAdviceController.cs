using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace Farm_App.Controllers;

public class FeedAdviceController : Controller
{
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FeedAdviceController> _logger;

    public FeedAdviceController(IHttpClientFactory clients, IConfiguration configuration, ILogger<FeedAdviceController> logger)
    {
        _clients = clients;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FindPrice([FromBody] FeedPriceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProductName))
            return BadRequest(new { error = "Choose a feed product first." });

        // Always attempt direct supplier searches first. This path works without OpenAI credits.
        var directResult = await SearchSupplierPrices(request.ProductName, request.BagKg);
        if (directResult != null) return Ok(directResult);

        var key = _configuration["OpenAI:ApiKey"] ?? _configuration["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
            return StatusCode(503, new { error = "AI price search is not configured." });

        var model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        var input = new
        {
            model,
            tools = new[] { new { type = "web_search" } },
            max_output_tokens = 900,
            instructions = "You are researching current livestock feed prices for a farmer in Namibia. Use web search now. Search the exact product name, prioritizing official Feedmaster Namibia pages and Namibian suppliers. Never invent or infer a price. Distinguish NAD/N$ from South African rand (ZAR/R), and do not convert currencies unless you state the exchange-rate source and date. Return a concise report with: product and bag size; exact price and currency only if an accessible source explicitly lists it; seller; direct source URL; date/source freshness; and whether it is a retail price or quote-only. If no reliable current public price can be verified, say so clearly and provide the official supplier/contact page or phone if found. Do not treat old, unrelated, or South African prices as a current Namibian price. End with one machine-readable line exactly like PRICE_JSON: {\"found\":true,\"price\":123.45,\"currency\":\"NAD\",\"bagKg\":50,\"sourceUrl\":\"https://example.com\",\"seller\":\"Seller\",\"asOf\":\"2026-10-21\"}. If no explicit verifiable NAD price is found, use found=false and price=null; never guess. The JSON line must be valid JSON.",
            input = $"Find the latest publicly verifiable price in Namibia for feed product: {request.ProductName}. Expected bag size if known: {request.BagKg?.ToString() ?? "not specified"} kg. Location: {request.Location ?? "Namibia"}. Current date: {DateTime.UtcNow:yyyy-MM-dd}."
        };

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            message.Content = new StringContent(JsonSerializer.Serialize(input), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(message);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var apiMessage = "The AI provider rejected the request.";
                try
                {
                    using var errorDoc = JsonDocument.Parse(json);
                    if (errorDoc.RootElement.TryGetProperty("error", out var errorElement) && errorElement.TryGetProperty("message", out var messageElement))
                        apiMessage = messageElement.GetString() ?? apiMessage;
                }
                catch (JsonException) { }
                _logger.LogWarning("OpenAI price search returned HTTP {StatusCode}: {ApiMessage}", (int)response.StatusCode, apiMessage);
                return StatusCode(502, new { error = $"AI price search returned HTTP {(int)response.StatusCode}: {apiMessage}" });
            }

            using var doc = JsonDocument.Parse(json);
            var output = new StringBuilder();
            if (doc.RootElement.TryGetProperty("output", out var items))
                foreach (var item in items.EnumerateArray())
                    if (item.TryGetProperty("content", out var content))
                        foreach (var part in content.EnumerateArray())
                            if (part.TryGetProperty("text", out var text)) output.AppendLine(text.GetString());
            var report = output.ToString().Trim();
            if (string.IsNullOrWhiteSpace(report))
                return StatusCode(502, new { error = "AI price search returned no results. Please try again." });

            decimal? price = null;
            decimal? bagKg = null;
            string? currency = null;
            string? sourceUrl = null;
            string? seller = null;
            string? asOf = null;
            var found = false;
            var match = Regex.Match(report, @"PRICE_JSON:\s*(\{[^\r\n]+\})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                try
                {
                    using var priceDoc = JsonDocument.Parse(match.Groups[1].Value);
                    var root = priceDoc.RootElement;
                    found = root.TryGetProperty("found", out var f) && f.ValueKind == JsonValueKind.True;
                    if (root.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var parsedPrice)) price = parsedPrice;
                    if (root.TryGetProperty("bagKg", out var b) && b.ValueKind == JsonValueKind.Number && b.TryGetDecimal(out var parsedKg)) bagKg = parsedKg;
                    if (root.TryGetProperty("currency", out var c)) currency = c.GetString();
                    if (root.TryGetProperty("sourceUrl", out var u)) sourceUrl = u.GetString();
                    if (root.TryGetProperty("seller", out var s)) seller = s.GetString();
                    if (root.TryGetProperty("asOf", out var a)) asOf = a.GetString();
                }
                catch (JsonException) { found = false; price = null; }
            }
            // Only offer a price for automatic application when the result explicitly reports NAD and a source URL.
            var applicable = found && price > 0 && string.Equals(currency, "NAD", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
            return Ok(new { report, found = applicable, price = applicable ? price : null, currency, bagKg, sourceUrl, seller, asOf, message = applicable ? "A source-listed NAD price was found. Review the source before applying it." : "No verified current NAD price was found that can safely be applied automatically. Review the search report and request a supplier quote if needed." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { error = "AI price search timed out. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI feed price search failed.");
            return StatusCode(502, new { error = "Could not search feed prices right now." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recommend([FromBody] FeedAdviceRequest request)
    {
        if (request.Animals == null || request.Animals.Count == 0)
            return BadRequest(new { error = "Add at least one animal group first." });

        var animals = request.Animals
            .Where(a => a.Count > 0 && a.BodyMassKg > 0)
            .Take(12)
            .ToList();
        if (animals.Count == 0)
            return BadRequest(new { error = "Enter a positive animal count and body mass." });

        if (request.FeedingDaysPerWeek is < 1 or > 7)
            return BadRequest(new { error = "Feeding days per week must be between 1 and 7." });

        var key = _configuration["OpenAI:ApiKey"] ?? _configuration["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            var supplementProduct = Regex.IsMatch(request.ProductName ?? "", @"\b(lick|block|concentrate|futterfos|veefos|optimizer|cattle mix)\b", RegexOptions.IgnoreCase);
            if (string.Equals(request.FeedBasis, "supplement", StringComparison.OrdinalIgnoreCase) || supplementProduct)
            {
                return Ok(new
                {
                    mode = "built-in guidance",
                    configured = false,
                    message = "AI is not configured. This looks like a lick, block, or concentrate, so a generic body-mass percentage would be unsafe. Follow the exact bag-label feeding or mixing instructions. Enter a verified kg/head rate in supplement mode to calculate costs, or configure AI and include the label directions for more specific guidance.",
                    recommendations = Array.Empty<object>()
                });
            }

            var guidance = animals.Select(a =>
            {
                var percent = a.Species.ToLowerInvariant() switch
                {
                    "sheep" or "goats" => 2.5m,
                    "cattle" or "calves" => 2.0m,
                    "horses" => 1.8m,
                    "poultry" => 3.0m,
                    "pigs" => 2.5m,
                    _ => 2.0m
                };
                return new
                {
                    species = a.Species,
                    count = a.Count,
                    bodyMassKg = a.BodyMassKg,
                    suggestedPercentBodyMass = percent,
                    estimatedKgPerFeedingDay = Math.Round(a.Count * a.BodyMassKg * percent / 100m, 2)
                };
            }).ToList();
            return Ok(new
            {
                mode = "built-in guidance",
                configured = false,
                message = "AI service is not connected yet. These are conservative starting estimates only, not a product-specific ration. Configure OpenAI:ApiKey to enable AI recommendations. Check the exact Feedmaster product label or ask a Feedmaster nutritionist, especially for licks and concentrates.",
                recommendations = guidance
            });
        }

        var model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        var input = new
        {
            model,
            max_output_tokens = 700,
            instructions = "You are a cautious livestock feed-planning assistant for Namibian farmers. Give practical preliminary guidance only. Never invent product-label directions or imply a lick, block, mineral supplement, or concentrate is a complete ration. Feed basis is supplied with the request: for complete feed, discuss body-mass percentages only as rough estimates; for supplement mode, interpret enteredRate as kg per head per feeding day and prioritize the exact label. If product type, label directions, grazing/forage, age, production stage, or body condition are missing, state what is missing rather than inventing a rate. Explain that dry-matter intake ranges are not the same as concentrate allowance. Do not prescribe unsafe abrupt ration changes.",
            input = JsonSerializer.Serialize(new
            {
                feedProduct = request.ProductName,
                feedBasis = request.FeedBasis,
                feedPriceNadPerBag = request.BagPrice,
                bagSizeKg = request.BagKg,
                feedingDaysPerWeek = request.FeedingDaysPerWeek,
                animals,
                additionalContext = request.Context
            })
        };

        try
        {
            // Use a dedicated HttpClient for this long-running AI call. The app-wide
            // standard resilience pipeline applies a short total-request timeout to
            // factory clients, which can cancel this request before HttpClient.Timeout.
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            message.Content = new StringContent(JsonSerializer.Serialize(input), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(message);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var apiMessage = "The AI provider rejected the request.";
                try
                {
                    using var errorDoc = JsonDocument.Parse(json);
                    if (errorDoc.RootElement.TryGetProperty("error", out var errorElement) &&
                        errorElement.TryGetProperty("message", out var messageElement) &&
                        messageElement.ValueKind == JsonValueKind.String)
                        apiMessage = messageElement.GetString() ?? apiMessage;
                }
                catch (JsonException) { }

                _logger.LogWarning("OpenAI Responses API returned HTTP {StatusCode}: {ApiMessage}", (int)response.StatusCode, apiMessage);
                return StatusCode(502, new { error = $"AI provider returned HTTP {(int)response.StatusCode}: {apiMessage}" });
            }

            using var doc = JsonDocument.Parse(json);
            var output = new StringBuilder();
            if (doc.RootElement.TryGetProperty("output", out var items))
                foreach (var item in items.EnumerateArray())
                    if (item.TryGetProperty("content", out var content))
                        foreach (var part in content.EnumerateArray())
                            if (part.TryGetProperty("text", out var text)) output.AppendLine(text.GetString());

            var recommendation = output.ToString().Trim();
            if (string.IsNullOrWhiteSpace(recommendation))
                return StatusCode(502, new { error = "AI returned no recommendation. Please try again." });

            return Ok(new { mode = "AI", configured = true, recommendation });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { error = "AI recommendation timed out. Please try again." });
        }
        catch
        {
            return StatusCode(502, new { error = "Could not obtain an AI recommendation right now." });
        }
    }

    private static async Task<object?> SearchSupplierPrices(string productName, decimal? expectedBagKg)
    {
        // Public web search fallback: does not use the OpenAI API or consume API credits.
        var queries = new[]
        {
            $"\"{productName}\" price Namibia NAD feed bag",
            $"site:feedmaster.com.na \"{productName}\"",
            $"Namibia livestock feed suppliers price \"{productName}\""
        };
        var results = new List<(string Title, string Url, string Snippet)>();
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36");
            foreach (var query in queries)
            {
                try
                {
                    var url = "https://www.google.com/search?q=" + Uri.EscapeDataString(query);
                    var html = await client.GetStringAsync(url);
                    foreach (Match block in Regex.Matches(html, @"<div class=""MjjYud""[\s\S]*?(?=<div class=""MjjYud""|$)", RegexOptions.IgnoreCase))
                    {
                        var chunk = block.Value;
                        var link = Regex.Match(chunk, @"<a href=""/url?q=([^&""]+)[^""]*""", RegexOptions.IgnoreCase);
                        if (!link.Success) link = Regex.Match(chunk, @"<a href=""(https?://[^""]+)""", RegexOptions.IgnoreCase);
                        var heading = Regex.Match(chunk, @"<h3[^>]*>([\s\S]*?)</h3>", RegexOptions.IgnoreCase);
                        if (!link.Success || !heading.Success) continue;
                        var resultUrl = WebUtility.HtmlDecode(link.Groups[1].Value);
                        if (resultUrl.StartsWith("/url?q=", StringComparison.Ordinal)) resultUrl = resultUrl[7..];
                        var title = WebUtility.HtmlDecode(Regex.Replace(heading.Groups[1].Value, "<[^>]+>", " ")).Trim();
                        var snippetMatch = Regex.Match(chunk, @"<div class=""(?:VwiC3b|yXK7lf)[^""]*""[^>]*>([\s\S]*?)</div>", RegexOptions.IgnoreCase);
                        var snippet = snippetMatch.Success ? WebUtility.HtmlDecode(Regex.Replace(snippetMatch.Groups[1].Value, "<[^>]+>", " ")).Trim() : "";
                        if (Uri.TryCreate(resultUrl, UriKind.Absolute, out var parsed) && (parsed.Scheme == "https" || parsed.Scheme == "http") &&
                            !results.Any(x => x.Url == resultUrl))
                            results.Add((title, resultUrl, snippet));
                    }
                }
                catch { /* Continue with other queries if a search provider blocks one. */ }
            }
        }
        catch { }

        var official = "https://www.feedmaster.com.na/contact-us";
        var report = new StringBuilder();
        report.AppendLine($"Direct supplier web search for {productName} in Namibia (expected bag size: {(expectedBagKg?.ToString() ?? "not specified")} kg).");
        report.AppendLine("No price is applied automatically unless a current, explicit NAD price can be verified from the source.");
        if (results.Count == 0)
        {
            report.AppendLine("The search provider did not return readable results from this connection. No price could be verified.");
        }
        else
        {
            foreach (var item in results.Take(8))
            {
                report.AppendLine();
                report.AppendLine(item.Title);
                if (!string.IsNullOrWhiteSpace(item.Snippet)) report.AppendLine(item.Snippet);
                report.AppendLine(item.Url);
            }
            report.AppendLine();
            report.AppendLine("Search results are leads, not confirmation of current stock or pricing. Open the supplier source and confirm the bag size, currency, and date before purchasing.");
        }
        report.AppendLine();
        report.AppendLine("Official Feedmaster contact: " + official);
        return new
        {
            report = report.ToString().Trim(),
            found = false,
            price = (decimal?)null,
            currency = (string?)null,
            bagKg = expectedBagKg,
            sourceUrl = (string?)null,
            seller = (string?)null,
            asOf = (string?)null,
            message = "Supplier search ran without using OpenAI credits. No verified current NAD bag price was safe to apply automatically; check the source links or request a supplier quote."
        };
    }

    public sealed class FeedPriceRequest
    {
        public string? ProductName { get; set; }
        public decimal? BagKg { get; set; }
        public string? Location { get; set; } = "Namibia";
    }

    public sealed class FeedAdviceRequest
    {
        public List<FeedAnimal> Animals { get; set; } = [];
        public string? ProductName { get; set; }
        public decimal? BagPrice { get; set; }
        public decimal? BagKg { get; set; }
        public int FeedingDaysPerWeek { get; set; } = 7;
        public string FeedBasis { get; set; } = "complete";
        public string? Context { get; set; }
    }

    public sealed class FeedAnimal
    {
        public string Species { get; set; } = "Other";
        public int Count { get; set; }
        public decimal BodyMassKg { get; set; }
        public decimal EnteredRate { get; set; }
    }
}
