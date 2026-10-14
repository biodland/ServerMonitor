using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServerMonitor.Infrastructure.Storage;

namespace ServerMonitor.Services;

/// <summary>
/// Decimates the live collector snapshots into SQLite (DESIGN.md Phase 5).
/// Runs on its own slow tick so storage I/O can never stall the 5s/2s
/// collection loops; only snapshots newer than the last write are stored.
/// Retention pruning runs at most once per hour.
/// </summary>
public class HistoryPersistenceService : BackgroundService
{
    private readonly ILogger<HistoryPersistenceService> _logger;
    private readonly IMetricsCollectorService _metrics;
    private readonly ISystemStatsService _stats;
    private readonly IHistoryStore _store;
    private readonly TimeSpan _interval;
    private readonly int _retentionDays;

    private DateTime _lastMetricsWrite = DateTime.MinValue;
    private DateTime _lastStatsWrite = DateTime.MinValue;
    private DateTime _lastPrune = DateTime.MinValue;

    public HistoryPersistenceService(
        ILogger<HistoryPersistenceService> logger,
        IMetricsCollectorService metrics,
        ISystemStatsService stats,
        IHistoryStore store,
        IConfiguration configuration)
    {
        _logger = logger;
        _metrics = metrics;
        _stats = stats;
        _store = store;

        var seconds = configuration.GetValue<int?>("ServerMonitor:Storage:WriteIntervalSeconds") ?? 60;
        _interval = TimeSpan.FromSeconds(Math.Clamp(seconds, 5, 3600));
        _retentionDays = configuration.GetValue<int?>("ServerMonitor:Storage:RetentionDays") ?? 30;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _store.InitializeAsync(stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        if (!_store.IsAvailable)
        {
            _logger.LogInformation("HistoryPersistenceService idle (storage unavailable or disabled)");
            return;
        }

        _logger.LogInformation("HistoryPersistenceService started (every {Interval}s, retention {Retention}d)",
            _interval.TotalSeconds, _retentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }

            try
            {
                await WriteSnapshotsAsync(stoppingToken);
                await PruneIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting history snapshots");
            }
        }
    }

    private async Task WriteSnapshotsAsync(CancellationToken ct)
    {
        var metrics = _metrics.Latest;
        if (metrics != null && metrics.Timestamp > _lastMetricsWrite)
        {
            await _store.AppendMetricsAsync(metrics, ct);
            _lastMetricsWrite = metrics.Timestamp;
        }

        var stats = _stats.Latest;
        if (stats != null && stats.Timestamp > _lastStatsWrite)
        {
            await _store.AppendStatsAsync(stats, ct);
            _lastStatsWrite = stats.Timestamp;
        }
    }

    private async Task PruneIfDueAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastPrune < TimeSpan.FromHours(1)) return;
        _lastPrune = DateTime.UtcNow;

        var removed = await _store.PruneAsync(DateTime.UtcNow.AddDays(-_retentionDays), ct);
        if (removed > 0)
            _logger.LogInformation("Pruned {Removed} history rows older than {Retention}d", removed, _retentionDays);
    }
}
