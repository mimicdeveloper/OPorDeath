using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OPorDie.Components;   // the Blazor UI (App.razor lives here)
using OPorDie.Data;
using OPorDie.Models;
using OPorDie.Services;

// =====================================================================
// Program.cs is the ENTRY POINT. It runs top-to-bottom once at startup.
// Two phases:
//   PHASE 1 (builder): register the "services" (tools) your app can use.
//   PHASE 2 (app):     define the URL endpoints and start listening.
// =====================================================================

var builder = WebApplication.CreateBuilder(args);

// Run on port 5099. (macOS uses port 5000 for its AirPlay Receiver, which
// answers browser requests with a 403 — so we avoid 5000 entirely.)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5099";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// --- PHASE 1: register services into the dependency-injection container ---
// "Dependency injection" just means: you list the tools here once, and the
// framework hands them to whoever asks for them. You don't 'new' them by hand.

// Register the database. It reads the connection string from appsettings.json
// and uses SQLite (a single .db file on disk).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Register our pack simulator so endpoints AND Blazor pages can ask for it.
builder.Services.AddSingleton<PackSimulator>();

// HttpClient + the card importer (pulls the full card list from optcgapi.com).
builder.Services.AddHttpClient();
builder.Services.AddScoped<CardImporter>();
builder.Services.AddScoped<BandaiScraper>(); // official-site full importer

// Background job: auto-loads the full card database on first startup.
builder.Services.AddHostedService<StartupImporter>();

// Swagger = the auto-generated test page (handy while learning).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Authentication (ASP.NET Core Identity) ---
// Separate users database so accounts never touch your card/deck data.
builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("UsersDb")));

// AddDefaultIdentity wires secure password hashing, cookie sign-in, and the
// built-in Login/Register/Manage/Forgot-password pages (under /Identity/Account/*).
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false; // no email step for now
        options.Password.RequiredLength = 8;            // stronger minimum
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;    // brute-force protection
    })
    .AddEntityFrameworkStores<UsersDbContext>();

builder.Services.AddRazorPages(); // serves the Identity account pages

// --- Blazor: register the UI engine ---
// AddRazorComponents turns on Blazor. AddInteractiveServerComponents means
// button clicks run your C# on the server over a live connection (no JS needed).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// On startup: create the database file if needed, then seed sample cards.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();   // makes the tables if they don't exist
    CardSeeder.SeedIfEmpty(db);    // fills in sample cards on first run

    // Create the Identity (users) database tables on first run.
    var usersDb = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    usersDb.Database.EnsureCreated();
}

// Serve the Blazor JS/CSS files (from wwwroot and the framework).
app.UseStaticFiles();

// Routing must come BEFORE antiforgery so antiforgery can see which endpoint
// it's protecting. Getting this order wrong is a classic cause of a 403.
app.UseRouting();

// Authentication must run after routing and before the endpoints/antiforgery.
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(); // visit http://localhost:5000/swagger to click around

app.UseAntiforgery(); // security middleware Blazor needs (after UseRouting)

// --- PHASE 2: define endpoints (the URLs your app responds to) ---
// Each app.MapGet/MapPost says: "when this URL is hit, run this function".

// List every set.
app.MapGet("/api/sets", async (AppDbContext db) =>
    await db.CardSets.ToListAsync());

// List every card (optionally filter by set code: /api/cards?set=OP01).
app.MapGet("/api/cards", async (AppDbContext db, string? set) =>
{
    var query = db.Cards.AsQueryable();
    if (!string.IsNullOrEmpty(set))
        query = query.Where(c => c.CardSet!.Code == set);
    return await query.ToListAsync();
});

// Get one card by its number, e.g. /api/cards/OP01-024.
app.MapGet("/api/cards/{number}", async (AppDbContext db, string number) =>
{
    var card = await db.Cards.FirstOrDefaultAsync(c => c.CardNumber == number);
    return card is null ? Results.NotFound() : Results.Ok(card);
});

// THE SIMULATOR ENDPOINT: open a pack from a set, e.g. POST /api/packs/open?set=OP01
app.MapPost("/api/packs/open", async (AppDbContext db, PackSimulator sim, string set) =>
{
    var cards = await db.Cards.Where(c => c.CardSet!.Code == set).ToListAsync();
    if (cards.Count == 0)
        return Results.NotFound($"No cards found for set '{set}'.");

    var pulled = sim.OpenPack(cards);

    // Return a tidy summary the front-end can show.
    return Results.Ok(new
    {
        set,
        totalValue = pulled.Sum(c => c.MarketPrice),
        cards = pulled.Select(c => new { c.CardNumber, c.Name, c.Rarity, c.MarketPrice })
    });
});

// See the whole collection.
app.MapGet("/api/collection", async (AppDbContext db) =>
    await db.CollectionItems.Include(ci => ci.Card).ToListAsync());

// Add a card to the collection by card number (POST /api/collection?number=OP01-024).
// If it's already there, just bump the quantity.
app.MapPost("/api/collection", async (AppDbContext db, string number) =>
{
    var card = await db.Cards.FirstOrDefaultAsync(c => c.CardNumber == number);
    if (card is null) return Results.NotFound($"Card '{number}' not found.");

    var existing = await db.CollectionItems.FirstOrDefaultAsync(ci => ci.CardId == card.Id);
    if (existing is null)
        db.CollectionItems.Add(new CollectionItem { CardId = card.Id, Quantity = 1 });
    else
        existing.Quantity++;

    await db.SaveChangesAsync();
    return Results.Ok();
});

// --- Turn on the Blazor UI ---
// This says: "render the App component and let its pages run interactively."
// The homepage of your app is now the Blazor UI; the /api/... routes above
// and /swagger still work alongside it.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map the Identity account pages (Login, Register, Manage, Forgot password…).
app.MapRazorPages();

app.MapPost("/account/logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
});

app.Run(); // starts the web server and blocks here, listening for requests
