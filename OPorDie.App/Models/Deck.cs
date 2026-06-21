namespace OPorDie.Models;

// A saved deck: a name, a Leader card, and a list of 50 cards.
// We store cards by their CARD NUMBER (not a DB foreign key) so that imported
// tournament lists work even for cards that aren't in your local sample DB yet.
public class Deck
{
    public int Id { get; set; }
    public string Name { get; set; } = "Untitled Deck";

    // The Leader card number, e.g. "OP01-001". One per deck.
    public string LeaderCardNumber { get; set; } = "";
    public string LeaderName { get; set; } = "";

    public List<DeckCard> Cards { get; set; } = new();
}

// One line of a deck: "4 copies of OP01-016".
public class DeckCard
{
    public int Id { get; set; }

    public int DeckId { get; set; }
    public Deck? Deck { get; set; }

    public string CardNumber { get; set; } = "";
    public string CardName { get; set; } = "";
    public int Quantity { get; set; } = 1;
}
