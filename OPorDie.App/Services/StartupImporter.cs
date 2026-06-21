using Microsoft.EntityFrameworkCore;
using OPorDie.Data;

namespace OPorDie.Services;

// Runs once in the background when the app starts. If the database only has the
// handful of sample cards, it pulls the FULL card list from optcgapi.com so the
// Deck Builder and Browse have every card with no manual step. It runs on a
// background thread so it never slows down startup, and it's safe to fail
// (you can always use the Import Cards page to retry).
public class StartupImporter : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<StartupImporter> _log;

    public StartupImporter(IServiceProvider services, ILogger<StartupImporter> log)
    {
        _services = services;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Fire-and-forget so the web server starts immediately.
        _ = Task.Run(RunAsync);
        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // If we already have a real card pool, don't re-download.
            if (await db.Cards.CountAsync() > 100) return;

            _log.LogInformation("Auto-importing full card database from optcgapi.com…");
            var importer = scope.ServiceProvider.GetRequiredService<CardImporter>();
            var n = await importer.ImportAllAsync();
            _log.LogInformation("Auto-import complete: {Count} cards.", n);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Auto-import failed; use the Import Cards page to retry.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
