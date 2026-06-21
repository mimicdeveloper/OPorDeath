using OPorDie.Data;
using OPorDie.Models;

namespace OPorDie.Services;

// In a real app this class would call a card API (like optcgapi.com) and
// import every set. For the MVP we SEED a small, realistic sample so the
// app runs offline with zero dependencies. Swap this out for a real HTTP
// fetch later — the rest of the app won't need to change.
public static class CardSeeder
{
    // Called once at startup. If the database already has cards, it does nothing.
    public static void SeedIfEmpty(AppDbContext db)
    {
        if (db.Cards.Any()) return; // already seeded — leave it alone

        var op01 = new CardSet { Code = "OP01", Name = "Romance Dawn" };

        // A handful of cards across rarities so the pack simulator has
        // something to pull from. Rarity codes: C, UC, R, SR, SEC, L.
        op01.Cards = new List<Card>
        {
            new() { CardNumber = "OP01-001", Name = "Roronoa Zoro",      Rarity = "L",   Color = "Green", CardType = "Leader",    MarketPrice = 4.00m },
            new() { CardNumber = "OP01-024", Name = "Monkey D. Luffy",   Rarity = "SEC", Color = "Red",   CardType = "Character", MarketPrice = 120.00m },
            new() { CardNumber = "OP01-025", Name = "Trafalgar Law",     Rarity = "SR",  Color = "Green", CardType = "Character", MarketPrice = 18.00m },
            new() { CardNumber = "OP01-016", Name = "Nico Robin",        Rarity = "SR",  Color = "Purple",CardType = "Character", MarketPrice = 9.00m },
            new() { CardNumber = "OP01-006", Name = "Usopp",             Rarity = "R",   Color = "Green", CardType = "Character", MarketPrice = 1.50m },
            new() { CardNumber = "OP01-013", Name = "Carrot",            Rarity = "R",   Color = "Red",   CardType = "Character", MarketPrice = 1.25m },
            new() { CardNumber = "OP01-070", Name = "Sanji",             Rarity = "UC",  Color = "Blue",  CardType = "Character", MarketPrice = 0.75m },
            new() { CardNumber = "OP01-040", Name = "Tony Tony Chopper", Rarity = "UC",  Color = "Black", CardType = "Character", MarketPrice = 0.50m },
            new() { CardNumber = "OP01-093", Name = "Gum-Gum Pistol",    Rarity = "C",   Color = "Red",   CardType = "Event",     MarketPrice = 0.10m },
            new() { CardNumber = "OP01-094", Name = "Diable Jambe",      Rarity = "C",   Color = "Blue",  CardType = "Event",     MarketPrice = 0.10m },
            new() { CardNumber = "OP01-031", Name = "Jinbe",             Rarity = "C",   Color = "Blue",  CardType = "Character", MarketPrice = 0.15m },
            new() { CardNumber = "OP01-051", Name = "Crocodile",         Rarity = "C",   Color = "Purple",CardType = "Character", MarketPrice = 0.20m },
        };

        // Fill in the official public image URL for every card from its number,
        // so photos show up across the whole UI with no extra data entry.
        foreach (var c in op01.Cards)
            c.ImageUrl = CardImages.Url(c.CardNumber);

        db.CardSets.Add(op01); // adding the set also adds its cards (cascade)
        db.SaveChanges();      // writes everything to the SQLite file
    }
}
