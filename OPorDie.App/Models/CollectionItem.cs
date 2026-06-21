namespace OPorDie.Models;

// One line in a user's collection: "I own 3 copies of OP01-001".
// In the MVP we keep it simple — no login yet, just one collection.
// Later you'd add a UserId column here to support many users.
public class CollectionItem
{
    public int Id { get; set; }

    // Which card this row is about (foreign key to Card).
    public int CardId { get; set; }
    public Card? Card { get; set; }

    // How many copies the user owns.
    public int Quantity { get; set; } = 1;
}
