namespace OPorDie.Models;

// One card, e.g. "Monkey D. Luffy" (OP01-001), a Leader, rarity "L".
public class Card
{
    public int Id { get; set; }

    // The printed card number, e.g. "OP01-001". Unique per card.
    public string CardNumber { get; set; } = "";

    public string Name { get; set; } = "";

    // Rarity drives the pack simulator's pull rates. We keep it as a
    // simple string code: "C", "UC", "R", "SR", "SEC", "L".
    public string Rarity { get; set; } = "C";

    // Optional gameplay/info fields — fine to leave blank in the MVP.
    public string Color { get; set; } = "";
    public string CardType { get; set; } = ""; // Leader / Character / Event / Stage
    public string ImageUrl { get; set; } = "";

    // Gameplay numbers (filled in by the importer; sample cards leave them 0).
    public int Cost { get; set; }
    public int Power { get; set; }
    public int Counter { get; set; }
    public int Life { get; set; }          // leaders only

    // Text/flavor fields.
    public string Effect { get; set; } = "";
    public string Attribute { get; set; } = ""; // Slash, Strike, Ranged, etc.
    public string Traits { get; set; } = "";    // e.g. "Straw Hat Crew / Supernovas"

    // A rough market price, used later by the price-alerts feature.
    public decimal MarketPrice { get; set; }

    // FOREIGN KEY: which set this card belongs to. These two properties
    // together tell EF Core "every Card links back to one CardSet".
    public int CardSetId { get; set; }
    public CardSet? CardSet { get; set; }
}
