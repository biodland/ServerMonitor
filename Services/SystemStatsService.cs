using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Services;

/// <summary>
/// Background service that periodically collects system statistics
/// (CPU / memory / network / storage) and maintains a rolling history.
///
/// Network interface filtering can be configured via:
///   ServerMonitor:Stats:Network:Include  (string[] - whitelist; if set only these are reported)
///   ServerMonitor:Stats:Network:Exclude  (string[] - blacklist; default excludes virtual ifs)
/// Both support glob-style "*" wildcards (e.g. "veth*", "br-*", "docker*").
/// </summary>
public class SystemStatsService : BackgroundService, ISystemStatsService
{
    private readonly ILogger<SystemStatsService> _logger;
    private readonly ISystemStatsCollector _collector;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(2);
    private const int MaxHistorySize = 300; // 10 minutes at 2s

    private readonly object _lock = new();
    private readonly LinkedList<SystemStats> _history = new();
    private SystemStats? _latest;

    private List<string>? _includePatterns;
    private List<string>? _excludePatterns;

    public SystemStats? Latest
    {
        get { lock (_lock) return _latest; }
    }

    public IReadOnlyList<SystemStats> History
    {
        get { lock (_lock) return _history.ToArray(); }
    }

    public SystemStatsService(
        ILogger<SystemStatsService> logger,
        ISystemStatsCollector collector,
        IConfiguration configuration)
    {
        _logger = logger;
        _collector = collector;
        _configuration = configuration;
        LoadFilters();
    }

    private void LoadFilters()
    {
        var include = _configuration.GetSection("ServerMonitor:Stats:Network:Include")
            .Get<string[]>();
        var exclude = _configuration.GetSection("ServerMonitor:Stats:Network:Exclude")
            .Get<string[]>();

        _includePatterns = include?.ToList();

        // Default exclude: common virtual interfaces
        _excludePatterns = exclude?.ToList() ?? new List<string>
        {
            "lo", "veth*", "br-*", "docker*", "virbr*", "vnet*", "tun*", "tap*", "wg*"
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SystemStatsService started (interval: {Interval}s)",
            _interval.TotalSeconds);

        // Prime the collector so the first user-visible call has rates
        try { await RefreshAsync(stoppingToken); } catch { }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting system stats");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<SystemStats> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var stats = await _collector.CollectAsync(cancellationToken);

        // Apply network interface filtering
        stats.NetworkInterfaces = FilterInterfaces(stats.NetworkInterfaces);

        lock (_lock)
        {
            _latest = stats;
            _history.AddLast(stats);
            while (_history.Count > MaxHistorySize)
                _history.RemoveFirst();
        }

        return stats;
    }

    private List<NetworkInterfaceStats> FilterInterfaces(List<NetworkInterfaceStats> all)
    {
        IEnumerable<NetworkInterfaceStats> filtered = all;

        if (_includePatterns is { Count: > 0 })
        {
            filtered = filtered.Where(i => _includePatterns.Any(p => MatchesGlob(i.Name, p)));
        }

        if (_excludePatterns is { Count: > 0 })
        {
            filtered = filtered.Where(i => !_excludePatterns.Any(p => MatchesGlob(i.Name, p)));
        }

        return filtered.ToList();
    }

    private static bool MatchesGlob(string value, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        if (!pattern.Contains('*')) return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);

        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(value, regex,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
