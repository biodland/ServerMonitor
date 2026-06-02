using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Services;

/// <summary>
/// Periodic background metrics collector
/// Maintains a rolling history of metrics for graphing and APIs
/// </summary>
public class MetricsCollectorService : BackgroundService, IMetricsCollectorService
{
    private readonly ILogger<MetricsCollectorService> _logger;
    private readonly IServerProvider _provider;
    private readonly IGpuMonitor _gpuMonitor;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private const int MaxHistorySize = 120; // 10 minutes at 5s interval

    private readonly object _lock = new();
    private readonly LinkedList<ServerMetrics> _history = new();
    private ServerMetrics? _latest;

    public ServerMetrics? Latest
    {
        get { lock (_lock) return _latest; }
    }

    public IReadOnlyList<ServerMetrics> History
    {
        get { lock (_lock) return _history.ToArray(); }
    }

    public MetricsCollectorService(
        ILogger<MetricsCollectorService> logger,
        IServerProvider provider,
        IGpuMonitor gpuMonitor)
    {
        _logger = logger;
        _provider = provider;
        _gpuMonitor = gpuMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetricsCollectorService started (interval: {Interval}s)",
            _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<ServerMetrics> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new ServerMetrics
        {
            Timestamp = DateTime.UtcNow
        };

        // Server info (lightweight - cached after first call)
        try
        {
            metrics.Server = await _provider.GetServerInfoAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get server info");
        }

        // Run reads in parallel
        var tempsTask = SafeAsync(() => _provider.TemperatureMonitor.GetTemperaturesAsync(cancellationToken));
        var coresTask = SafeAsync(() => _provider.TemperatureMonitor.GetCpuCoreTemperaturesAsync(cancellationToken));
        var fansTask = SafeAsync(() => _provider.FanController.GetFanStatusAsync(cancellationToken));
        var powerTask = SafePowerAsync(cancellationToken);
        var gpuTask = SafeAsync(() => _gpuMonitor.GetGpuReadingsAsync(cancellationToken));

        await Task.WhenAll(tempsTask, coresTask, fansTask, powerTask, gpuTask);

        metrics.Temperatures = tempsTask.Result ?? new List<TemperatureReading>();
        metrics.CpuCores = coresTask.Result ?? new List<TemperatureReading>();
        metrics.Fans = fansTask.Result ?? new List<FanReading>();
        metrics.Power = powerTask.Result;
        metrics.Gpus = gpuTask.Result ?? new List<GpuReading>();

        lock (_lock)
        {
            _latest = metrics;
            _history.AddLast(metrics);
            while (_history.Count > MaxHistorySize)
                _history.RemoveFirst();
        }

        return metrics;
    }

    private async Task<List<T>?> SafeAsync<T>(Func<Task<List<T>>> action)
    {
        try { return await action(); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Metric read failed");
            return null;
        }
    }

    private async Task<PowerReading?> SafePowerAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_provider.PowerMonitor.IsSupported) return null;
            return await _provider.PowerMonitor.GetPowerMetricsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Power read failed");
            return null;
        }
    }
}
