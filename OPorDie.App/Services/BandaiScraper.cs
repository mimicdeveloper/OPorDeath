using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OPorDie.Data;
using OPorDie.Models;

namespace OPorDie.Services;

// Scrapes the OFFICIAL One Piece card list (en.onepiece-cardgame.com/cardlist).
// This is the only free source that has every set (incl. OP16), every field, and
// the parallel/alt-art versions. It works by:
//   1. loading the cardlist page and reading the "series" dropdown,
//   2. requesting each series and parsing the card blocks out of the HTML.
//
// HTML scraping is brittle: if Bandai changes their markup, the regexes need
// updating. SampleBlock holds the first raw card block so we can re-map quickly.
public class BandaiScraper
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AppDbContext _db;

    public BandaiScraper(IHttpClientFactory httpFactory, AppDbContext db)
    {
        _httpFactory = httpFactory;
        _db = db;
    }

    private const string Base = "https://en.onepiece-cardgame.com";
    private const string ListUrl = Base + "/cardlist/";

    public string SampleBlock { get; private set; } = "";
    public int SeriesFound { get; private set; }

    public async Task<int> ImportAllAsync()
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(90);
        // Use a browser-like User-Agent; the site may reject the default .NET one.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Safari/537.36");

        // 1) Get the list of series IDs from the dropdown on the main page.
        var home = await client.GetStringAsync(ListUrl);
        var seriesIds = ParseSeriesIds(home);
        SeriesFound = seriesIds.Count;
        if (seriesIds.Count == 0) seriesIds.Add(""); // fall back to the default page

        var setsByCode = await _db.CardSets.ToDictionaryAsync(s => s.Code);
        var cardsByNumber = await _db.Cards.ToDictionaryAsync(c => c.CardNumber);

        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var sid in seriesIds)
            {
                string html;
                try
                {
                    var url = string.IsNullOrEmpty(sid) ? ListUrl : $"{ListUrl}?series={sid}";
                    html = await client.GetStringAsync(url);
                }
                catch { continue; }

                foreach (var block in CardBlocks(html))
                {
                    var number = block.id;
                    if (string.IsNullOrEmpty(number)) continue;
                    if (SampleBlock == "") SampleBlock = block.html;

                    // Set code = part before the dash, ignoring any _pN parallel suffix.
                    var baseNum = number.Split('_')[0];
                    var setCode = baseNum.Contains('-') ? baseNum.Split('-')[0].ToUpperInvariant() : baseNum.ToUpperInvariant();
                    if (!setsByCode.TryGetValue(setCode, out var set))
                    {
                        set = new CardSet { Code = setCode, Name = setCode };
                        _db.CardSets.Add(set);
                        setsByCode[setCode] = set;
                    }

                    if (!cardsByNumber.TryGetValue(number, out var card))
                    {
                        card = new Card { CardNumber = number };
                        _db.Cards.Add(card);
                        cardsByNumber[number] = card;
                    }

                    var info = Between(block.html, "class=\"infoCol\"", "</div>");
                    var infoParts = Regex.Matches(info, @"<span[^>]*>(.*?)</span>", RegexOptions.Singleline)
                                         .Select(m => Clean(m.Groups[1].Value)).ToList();

                    card.Name = Clean(Group(block.html, @"class=""cardName""[^>]*>(.*?)</div>"));
                    card.Rarity = MapRarity(infoParts.Count > 1 ? infoParts[1] : "");
                    card.CardType = infoParts.Count > 2 ? infoParts[2] : "";
                    card.Color = Clean(Group(block.html, @"Color</h3>(.*?)</div>"));
                    card.Cost = ToInt(Group(block.html, @"Cost</h3>(.*?)</div>"));
                    card.Power = ToInt(Group(block.html, @"Power</h3>(.*?)</div>"));
                    card.Counter = ToInt(Group(block.html, @"Counter</h3>(.*?)</div>"));
                    card.Life = ToInt(Group(block.html, @"Life</h3>(.*?)</div>"));
                    card.Attribute = Clean(Group(block.html, @"Attribute</h3>(.*?)</div>"));
                    card.Traits = Clean(Group(block.html, @"(?:Type|Feature)</h3>(.*?)</div>"));
                    card.Effect = Clean(Group(block.html, @"Effect</h3>(.*?)</div>"));
                    card.ImageUrl = CardImages.Url(number); // proxy handles display
                    card.CardSet = set;
                }
                // We turned auto-detect off for speed, which means SaveChanges won't
                // pick up the card→set links on its own. Detect once here so the
                // foreign keys are filled in before saving.
                _db.ChangeTracker.DetectChanges();
                await _db.SaveChangesAsync();
            }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        return cardsByNumber.Count;
    }

    // ---- parsing helpers -------------------------------------------------

    private static List<string> ParseSeriesIds(string html)
    {
        var ids = new List<string>();
        // Isolate the series <select>, then read its <option value="..."> ids.
        var sel = Group(html, @"id=""series""(.*?)</select>");
        if (string.IsNullOrEmpty(sel)) sel = Group(html, @"name=""series""(.*?)</select>");
        foreach (Match m in Regex.Matches(sel, @"<option[^>]*value=""([^""]+)""", RegexOptions.Singleline))
        {
            var v = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(v)) ids.Add(v);
        }
        return ids.Distinct().ToList();
    }

    // Yields each card block: its id (card number) and inner HTML.
    private static IEnumerable<(string id, string html)> CardBlocks(string html)
    {
        // Match each <dl ... class="modalCol" ...> ... </dl>, capturing the open tag.
        foreach (Match m in Regex.Matches(html,
            @"<dl([^>]*class=""modalCol""[^>]*)>(.*?)</dl>", RegexOptions.Singleline))
        {
            var openTag = m.Groups[1].Value;
            var inner = m.Groups[2].Value;
            var id = Group(openTag, @"id=""([^""]+)""");
            yield return (id, inner);
        }
    }

    private static string Group(string input, string pattern)
    {
        var m = Regex.Match(input, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : "";
    }

    private static string Between(string input, string startMarker, string endMarker)
    {
        var i = input.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "";
        var j = input.IndexOf(endMarker, i, StringComparison.OrdinalIgnoreCase);
        return j < 0 ? input[i..] : input[i..j];
    }

    // Strip HTML tags + decode entities, collapse whitespace.
    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = Regex.Replace(s, @"<h3>.*?</h3>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", " ");
        s = WebUtility.HtmlDecode(s);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static int ToInt(string s)
    {
        var digits = Regex.Match(Clean(s), @"\d+");
        return digits.Success && int.TryParse(digits.Value, out var n) ? n : 0;
    }

    private static string MapRarity(string raw)
    {
        var r = raw.ToUpperInvariant().Trim();
        if (r.Contains("SEC")) return "SEC";
        if (r is "SR" || r.Contains("SUPER")) return "SR";
        if (r is "L" || r.Contains("LEADER")) return "L";
        if (r is "UC" || r.Contains("UNCOMMON")) return "UC";
        if (r is "R" || r.Contains("RARE")) return "R";
        if (r is "C" || r.Contains("COMMON")) return "C";
        return string.IsNullOrEmpty(r) ? "C" : r;
    }
}
