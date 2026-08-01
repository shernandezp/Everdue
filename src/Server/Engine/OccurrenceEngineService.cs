using Everdue.Server.Application.Abstractions;
using Everdue.Server.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Engine;

/// <summary>
/// The whole scheduler: one in-process timer. No Quartz, no Hangfire, no Redis, no cron — one
/// process is the entire system, which is what makes the install promise ("copy one file, run it")
/// true.
/// </summary>
public sealed class OccurrenceEngineService(
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    IOptions<EngineOptions> options,
    ILogger<OccurrenceEngineService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogWarning("Occurrence engine is disabled (Engine:Enabled=false). No occurrences will be created.");
            return;
        }

        var interval = TimeSpan.FromMinutes(options.Value.TickMinutes);
        logger.LogInformation("Occurrence engine started; ticking every {Minutes} minute(s).", options.Value.TickMinutes);

        // Run once at startup so a machine that was off overnight catches up before anyone logs in.
        await TickAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (!tenantContext.IsResolved)
        {
            logger.LogDebug("Skipping occurrence tick: the tenant is not resolved yet.");
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<OccurrenceEngine>();
            await engine.TickAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception e)
        {
            // A failed tick must never take the process with it: the next one re-derives everything.
            logger.LogError(e, "Occurrence tick failed. The next tick will retry from the same data.");
        }
    }
}
