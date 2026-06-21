using Microsoft.EntityFrameworkCore;
using OPorDie.Data;

namespace OPorDie.Services;

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
        _ = Task.Run(RunAsync);
        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (await db.Cards.CountAsync() > 100) return;

            _log.LogInformation("Auto-importing full card database from Bandai…");
            var scraper = scope.ServiceProvider.GetRequiredService<BandaiScraper>();
            var n = await scraper.ImportAllAsync();
            _log.LogInformation("Auto-import complete: {Count} cards.", n);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Auto-import from Bandai failed on startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
