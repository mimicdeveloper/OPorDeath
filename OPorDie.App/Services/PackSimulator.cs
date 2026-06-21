using OPorDie.Models;

namespace OPorDie.Services;

// THE "SIM" PIECE. This is the pack-OPENING simulator (not a dueling game).
// Give it the list of cards in a set, it returns a realistic 12-card pack
// by rolling each slot against published-style pull rates.
public class PackSimulator
{
    // One shared random number generator. Static so it isn't re-seeded
    // on every call (re-seeding too fast can produce repeats).
    private static readonly Random _rng = new();

    // A standard booster has 12 cards. The "rare slot" (the last one) is
    // where the chase cards live; the other 11 are commons/uncommons.
    public List<Card> OpenPack(List<Card> cardsInSet)
    {
        var pack = new List<Card>();

        // 11 "filler" slots: mostly commons, sometimes an uncommon.
        for (int i = 0; i < 11; i++)
        {
            var rarity = Roll(("C", 80), ("UC", 20));
            pack.Add(PickRandomOfRarity(cardsInSet, rarity));
        }

        // 1 "hit" slot — this is the exciting one. Weighted pull rates:
        // Rare most common, Secret Rare the jackpot.
        var hitRarity = Roll(("R", 70), ("SR", 24), ("SEC", 6));
        pack.Add(PickRandomOfRarity(cardsInSet, hitRarity));

        return pack;
    }

    // Roll takes pairs of (outcome, weight) and returns one outcome,
    // picked in proportion to its weight. Weights don't need to sum to 100.
    private static string Roll(params (string outcome, int weight)[] options)
    {
        int total = options.Sum(o => o.weight);
        int pick = _rng.Next(total);   // a number from 0 .. total-1
        int running = 0;
        foreach (var (outcome, weight) in options)
        {
            running += weight;
            if (pick < running) return outcome; // landed in this band
        }
        return options[0].outcome; // safety fallback (shouldn't happen)
    }

    // Find all cards of the rolled rarity and return one at random.
    // If the set has none of that rarity, fall back to a common so we
    // never crash on a missing rarity.
    private static Card PickRandomOfRarity(List<Card> cards, string rarity)
    {
        var pool = cards.Where(c => c.Rarity == rarity).ToList();
        if (pool.Count == 0)
            pool = cards.Where(c => c.Rarity == "C").ToList();
        if (pool.Count == 0)
            pool = cards; // last resort: any card at all

        return pool[_rng.Next(pool.Count)];
    }
}
