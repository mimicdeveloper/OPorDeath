using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OPorDie.Data;
using OPorDie.Models;

namespace OPorDie.Services;

// Pulls the FULL card list from the public optcgapi.com API into the local DB.
// It's written defensively: instead of trusting exact JSON field names, it
// detects the card number and image by pattern, so small naming differences
// in the API won't break the import. Run it from the Import page (a button).
public class CardImporter
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AppDbContext _db;

    public CardImporter(IHttpClientFactory httpFactory, AppDbContext db)
    {
        _httpFactory = httpFactory;
        _db = db;
    }

    // These endpoints return the booster/set cards and the structure-deck cards.
    private static readonly string[] Endpoints =
    {
        "https://optcgapi.com/api/allSetCards/",
        "https://optcgapi.com/api/allSTCards/",
        "https://optcgapi.com/api/allPromoCards/",
    };

    // Matches a printed card number like OP01-001, EB02-045, ST10-001, P-001.
    private static readonly Regex NumberRe =
        new(@"^([A-Z]{1,4}\d{2}-\d{3}|P-\d{3})$", RegexOptions.IgnoreCase);

    public string SampleJson { get; private set; } = "";   // first record, for debugging

    // Returns how many cards were imported. Throws on network errors (the page
    // shows the message).
    public async Task<int> ImportAllAsync()
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);

        // Load existing rows up front and track them by card number. This is the
        // key to avoiding duplicate-insert errors: One Piece has many alt-art /
        // parallel printings that share the same printed number, so we keep ONE
        // row per number and just update it instead of inserting a second.
        var setsByCode = await _db.CardSets.ToDictionaryAsync(s => s.Code);
        var cardsByNumber = await _db.Cards.ToDictionaryAsync(c => c.CardNumber);

        // SPEED: turn off automatic change-detection while we bulk-load thousands of
        // rows. With it on, EF re-scans every tracked entity on each Add/edit, which
        // makes a big import crawl. We re-enable it in the finally block.
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
        foreach (var url in Endpoints)
        {
            string json;
            try { json = await client.GetStringAsync(url); }
            catch { continue; } // skip an endpoint that fails, try the next

            using var doc = JsonDocument.Parse(json);
            var array = AsArray(doc.RootElement);
            if (array is null) continue;

            foreach (var el in array.Value.EnumerateArray())
            {
                if (SampleJson == "") SampleJson = el.ToString();

                var number = FindCardNumber(el);
                if (number is null) continue;

                var setCode = number.Split('-')[0].ToUpperInvariant();
                if (!setsByCode.TryGetValue(setCode, out var set))
                {
                    // SPEED: don't save here. We link via the navigation property
                    // below so EF inserts new sets together with the cards in one
                    // batch instead of a DB round-trip per set.
                    set = new CardSet { Code = setCode, Name = setCode };
                    _db.CardSets.Add(set);
                    setsByCode[setCode] = set;
                }

                // One row per number: reuse if we've already seen it this run/in DB.
                if (!cardsByNumber.TryGetValue(number, out var card))
                {
                    card = new Card { CardNumber = number };
                    _db.Cards.Add(card);
                    cardsByNumber[number] = card;
                }

                card.Name = Str(el, "card_name", "name", "cardName");
                card.Rarity = MapRarity(Str(el, "rarity", "card_rarity"));
                card.Color = Str(el, "card_color", "color");
                card.CardType = Str(el, "card_type", "type", "category");
                card.Cost = Num(el, "card_cost", "cost");
                card.Power = Num(el, "card_power", "power");
                card.Counter = Num(el, "counter", "card_counter", "counter_amount");
                card.Life = Num(el, "life", "card_life");
                card.Effect = Str(el, "card_text", "effect", "ability", "text");
                card.Attribute = Str(el, "attribute", "card_attribute");
                card.Traits = Str(el, "sub_types", "subtypes", "card_subtypes", "types", "card_traits");
                card.MarketPrice = Dec(el, "market_price", "price", "inventory_price");
                card.ImageUrl = FindImage(el) ?? CardImages.Url(number);
                card.CardSet = set; // navigation property → EF resolves the FK on save
            }
            // Auto-detect is off for speed, so force a detect here so the card→set
            // links are written before saving (otherwise the foreign key is missing).
            _db.ChangeTracker.DetectChanges();
            await _db.SaveChangesAsync(); // one batched save per endpoint
        }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        return cardsByNumber.Count;
    }

    // ---- helpers --------------------------------------------------------

    private static JsonElement? AsArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        // Some APIs wrap the list in an object like { "results": [...] }.
        foreach (var key in new[] { "results", "data", "cards" })
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty(key, out var inner) &&
                inner.ValueKind == JsonValueKind.Array)
                return inner;
        return null;
    }

    // Scan every string property and return the first that looks like a card number.
    private static string? FindCardNumber(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in el.EnumerateObject())
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var v = prop.Value.GetString() ?? "";
                if (NumberRe.IsMatch(v)) return v.ToUpperInvariant();
            }
        return null;
    }

    private static string? FindImage(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in el.EnumerateObject())
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var v = prop.Value.GetString() ?? "";
                if (v.StartsWith("http") && (v.EndsWith(".png") || v.EndsWith(".jpg")))
                    return v;
            }
        return null;
    }

    private static string Str(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";
        return "";
    }

    private static int Num(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (el.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var j)) return j;
            }
        return 0;
    }

    private static decimal Dec(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (el.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
                if (v.ValueKind == JsonValueKind.String &&
                    decimal.TryParse((v.GetString() ?? "").TrimStart('$'), out var e)) return e;
            }
        return 0;
    }

    // Turn "Super Rare" / "SR" etc. into our short codes used by the rarity chips.
    private static string MapRarity(string raw)
    {
        var r = raw.ToUpperInvariant();
        if (r.Contains("SECRET")) return "SEC";
        if (r.Contains("SUPER")) return "SR";
        if (r.Contains("LEADER") || r == "L") return "L";
        if (r.Contains("UNCOMMON") || r == "UC") return "UC";
        if (r.Contains("RARE") || r == "R") return "R";
        if (r.Contains("COMMON") || r == "C") return "C";
        return string.IsNullOrEmpty(raw) ? "C" : raw;
    }
}
