# OP or Death ☠ — One Piece TCG companion (backend API + Blazor UI)

A working C# / ASP.NET Core app with a **Blazor web UI** on top of the backend. Three of
the four core features are fully clickable today: **card data**, **collection tracker**,
and the **pack-opening simulator**. **Price alerts** has a UI stub, and the **Discord bot**
is a future step — both plug into this same base.

One project, one `dotnet run` serves everything:
- **The UI** at **http://localhost:5000** (the Blazor app — this is the main thing now).
- **The API** at `/api/...` and the test page at `/swagger` (still there for learning).

## The UI (Blazor) — what's where

Blazor lets you build the front-end in **C# instead of JavaScript**. Each page is a
`.razor` file: HTML markup on top, a `@code { }` C# block underneath for the logic.

| Page | URL | What it does |
|---|---|---|
| Dashboard | `/` | Live counts: cards, sets, cards owned, collection value |
| Browse Cards | `/cards` | **Search + filter** by color/type/rarity/cost, photo grid, click any card for a **full detail view** (effect text, power, counter, traits) |
| Deck Builder | `/deckbuilder` | Pick a Leader, click cards to add (photos), auto-enforces 4-copy/50-card rules, real cost curve, **save deck**, **copy OPTCGSim deck code**. Open a saved deck with `?deck=ID`. |
| My Decks | `/mydecks` | List, open, and delete your saved/imported decks |
| Top Decks | `/topdecks` | Current OP-16 meta tier list + links to live tournament lists, and a **paste-a-decklist importer** that renders images and saves the deck |
| Playtest | `/playtest` | **Single-player goldfish mat**: load a saved deck, draw, manage DON!!, play cards, attack a dummy life total. No opponent/auto-effects — use OPTCGSim for real matches |
| Pack Simulator | `/sim` | **Open Pack** button → rolls a real pack with photos, color-coded by rarity |
| My Collection | `/collection` | Everything you own, with photos and total value |
| Price Alerts | `/alerts` | Stub UI showing where the alerts feature will live |
| Import Cards | `/import` | **Pulls the full card list from optcgapi.com** so the Deck Builder has every card (images, cost, color). Run once after starting. |

### Deck codes (OPTCGSim)
Deck Builder's export is the exact format OPTCGSim imports — one `QTYxCARDNUMBER` per
line with the Leader first (e.g. `1xOP09-022`). Use the **Copy deck code** button, then in
OPTCGSim go to Deck Editor → *Import from Clipboard*.

### Card photos
Photos come from Bandai's public image URLs, built from the card number in
`Services/CardImages.cs` — no API key, no stored files. Any valid card number shows
its art, which is why the decklist importer can render *any* tournament list.

### About the "simulator"
Two different things, on purpose:
- **Playtest (`/playtest`)** — a solo practice mat we built. Great for testing a deck's
  curve and flow. It tracks game state (life, DON!!, hand, board, deck) but does **not**
  resolve card abilities automatically and has **no AI opponent**.
- **OPTCGSim** — the full two-player online sim with every ability coded. We don't rebuild
  that (it's a multi-year project); instead Deck Builder **exports your list** so you build
  here and play there.

Files live in `Components/`:
- `App.razor` — the root HTML page (rarely touched).
- `Routes.razor` — maps URLs to pages.
- `Layout/` — `MainLayout` (the frame) + `NavMenu` (the sidebar).
- `Pages/` — one file per screen above. **Start here when you want to change the UI.**
- `wwwroot/app.css` — all the styling (colors, layout). Edit freely.

How a page works (read `Pages/PackSim.razor` first — it's the clearest): the `@page`
line sets its URL, `@inject` hands it the database and simulator, `@onclick` connects a
button to a C# method, and changing a field automatically re-renders the screen. That
loop — click → C# runs → UI updates — is the whole idea of Blazor.

This is the **engine + UI**. Get the core right first, then add features.

---

## How to run it (3 commands)

You need the free **.NET 8 SDK** (https://dotnet.microsoft.com/download). Then, in a
terminal inside the `OPorDie.App` folder:

```bash
dotnet restore     # downloads the libraries listed in OPorDie.csproj
dotnet run         # builds and starts the web server
```

You'll see a line like `Now listening on: http://localhost:5000`. Open
**http://localhost:5000** in your browser — that's the **Blazor UI**. Click around the
sidebar: open packs on the Pack Simulator page, add cards, watch the dashboard totals
update.

Prefer the raw API? It's still at **http://localhost:5000/swagger**.

The first run creates a file `opordie.db` — that's your whole database, one file.
Leave the Terminal running while you use the app; press **Ctrl+C** to stop it.

> ⚠️ **After a schema change, delete `opordie.db` once before running.** The app only
> builds the database on first run, so when new columns/tables are added (e.g. card
> Cost/Power, the Decks tables) you must delete the file (it's just sample data) so it
> rebuilds. Then use the **Import Cards** page to reload the full card list:
> ```bash
> rm opordie.db
> dotnet run
> ```
> The app runs on **http://localhost:5099**.

---

## How each piece connects (the architecture)

Everything flows in one direction, which is what keeps it easy to reason about:

```
Browser / app  →  Endpoints (Program.cs)  →  Services  →  DbContext  →  SQLite file
                       │                        │             │
                  the URLs you hit        the "brains"   the door to data
```

- **Models/** (`Card`, `CardSet`, `CollectionItem`) — plain C# classes describing your
  data. EF Core turns each into a database table. Start here when you add a new concept.
- **Data/AppDbContext.cs** — the single object that talks to the database. Anything that
  reads or writes data asks it.
- **Services/CardSeeder.cs** — fills the database with sample cards on first run. **This
  is the seam where the real card API goes later** — replace the hard-coded list with an
  HTTP call to optcgapi.com and nothing else has to change.
- **Services/PackSimulator.cs** — the pack-opening logic (your "sim" feature). Pure C#,
  no database knowledge — you could unit-test it on its own.
- **Program.cs** — wires it all together and defines the endpoints (the URLs). This is
  the "front desk" that routes each request to the right code.

### Why it's split up like this
Each file has **one job**. When you want to change pull rates, you only touch
`PackSimulator.cs`. When you add a new field to a card, you only touch `Card.cs`. This
separation is the single most useful habit to build early — it's why big apps stay
manageable.

---

## How this feeds the business (tie-back to the plan)

- **Pack simulator** → the "Sim vs Real" video series. Open in the app, then open the
  real pack on camera. Direct line from a free feature to a product sale.
- **Collection tracker + price data** → daily reason to open the app → repeat audience →
  buyers and crowdfunding backers.
- The build itself → "Build Log" content. Filming yourself coding this *is* marketing.

---

## Your next learning steps (in order)

1. **Add a price field display.** Add a `LastChecked` date to `Card`, expose it in the
   cards endpoint. (Practice: model change → it appears in the API.)
2. **Replace the seeder with a real fetch.** Use `HttpClient` to pull cards from
   optcgapi.com inside `CardSeeder`. (Practice: calling an external API.)
3. **Price alerts.** New model `PriceAlert { CardId, TargetPrice }`, an endpoint to
   create one, and a background check. (Practice: background services.)
4. ~~A Blazor front-end.~~ ✅ Done — see `Components/`.
5. **Make Price Alerts real.** Add a `PriceAlert` model, save watches to the DB, and add
   a background service that re-checks prices. The UI stub at `/alerts` shows the shape.
6. **The Discord bot.** A separate small project using Discord.Net that calls this API.

Build one, get it working, then the next. Don't scaffold all of it at once.

## One note for later (not urgent)

The Blazor pages share a single database connection per browser session, which is fine
for one user. When you go multi-user, switch from `AddDbContext` to
`AddDbContextFactory<AppDbContext>` and create a short-lived context per action — the
standard Blazor pattern. Not needed while you're learning solo.
