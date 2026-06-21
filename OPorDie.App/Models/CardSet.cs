namespace OPorDie.Models;

// A "model" is just a C# class that describes ONE THING in your app.
// EF Core will turn this class into a database table called "CardSets",
// where each property below becomes a column.

// A CardSet = a One Piece release, e.g. "Romance Dawn" (code OP01).
public class CardSet
{
    // Id is the primary key. EF Core sees a property named "Id" and
    // automatically makes it the unique row identifier (auto-numbered).
    public int Id { get; set; }

    // "OP01", "OP02", etc. We use this in URLs (open a pack from OP01).
    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    // Navigation property: one set HAS MANY cards. EF Core uses this to
    // link the tables. You can write set.Cards to get every card in a set.
    public List<Card> Cards { get; set; } = new();
}
