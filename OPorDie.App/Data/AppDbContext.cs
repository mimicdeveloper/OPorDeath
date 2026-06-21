using Microsoft.EntityFrameworkCore;
using OPorDie.Models;

namespace OPorDie.Data;

// The DbContext is EF Core's "door" to your database.
// Think of it as the object you ask: "give me all cards", "save this collection".
// Each DbSet<T> below becomes a TABLE in the database.
public class AppDbContext : DbContext
{
    // The constructor receives configuration (like the connection string)
    // from Program.cs. You don't call this yourself — the framework does.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CardSet> CardSets => Set<CardSet>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<DeckCard> DeckCards => Set<DeckCard>();

    // OnModelCreating lets you fine-tune the table rules. Here we make
    // CardNumber unique so you can't accidentally store the same card twice.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>()
            .HasIndex(c => c.CardNumber)
            .IsUnique();

        modelBuilder.Entity<CardSet>()
            .HasIndex(s => s.Code)
            .IsUnique();
    }
}
