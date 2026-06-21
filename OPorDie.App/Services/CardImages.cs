namespace OPorDie.Services;

// Card photos come from Bandai's official, public image URLs. The pattern is
// fully predictable from the card number, so we never need to store images —
// we just build the URL. Example:
//   OP01-001  ->  https://en.onepiece-cardgame.com/images/cardlist/card/OP01-001.png
//
// (Card art is © Eiichiro Oda / Shueisha / Toei / Bandai — we only link to it.)
public static class CardImages
{
    // We use a public CORS-enabled image proxy instead of Bandai's site directly,
    // because the official site blocks "hotlinking" (loading its images from another
    // site), which makes the pictures fail to load. This proxy is meant for embedding.
    public static string Url(string cardNumber)
        => string.IsNullOrWhiteSpace(cardNumber)
            ? Placeholder
            : $"https://optcg-api.arjunbansal-ai.workers.dev/images/{cardNumber}";

    // A simple gray placeholder used if a number is blank.
    public const string Placeholder =
        "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='120' height='168'><rect width='100%25' height='100%25' fill='%23222'/></svg>";
}
