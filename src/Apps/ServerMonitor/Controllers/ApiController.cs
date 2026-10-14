using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;
using ServerMonitor.Infrastructure.Storage;
using ServerMonitor.Services;

namespace ServerMonitor.Controllers;

/// <summary>
/// REST API for accessing server metrics and controlling fans
/// </summary>
[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly IMetricsCollectorService _metrics;
    private readonly IServerProvider _provider;
    private readonly ISystemStatsService _stats;
    private readonly IHistoryStore _history;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        IMetricsCollectorService metrics,
        IServerProvider provider,
        ISystemStatsService stats,
        IHistoryStore history,
        ILogger<ApiController> logger)
    {
        _metrics = metrics;
        _provider = provider;
        _stats = stats;
        _history = history;
        _logger = logger;
    }

    /// <summary>Latest metrics snapshot</summary>
    [HttpGet("status")]
    public async Task<ActionResult<ServerMetrics>> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var metrics = _metrics.Latest;
            if (metrics == null)
            {
                metrics = await _metrics.RefreshAsync(cancellationToken);
            }
            
            // Mark as connected if we have data
            metrics.IsConnected = true;
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status");
            
            // Return last known state marked as disconnected
            var metrics = _metrics.Latest ?? new ServerMetrics();
            metrics.IsConnected = false;
            metrics.Timestamp = DateTime.UtcNow;
            
            return Ok(metrics);
        }
    }

    /// <summary>Information about the active server provider</summary>
    [HttpGet("provider")]
    public IActionResult GetProvider()
    {
        return Ok(new
        {
            _provider.Vendor,
            _provider.Model,
            _provider.DisplayName,
            FanControl = new
            {
                _provider.FanController.SupportsManualControl,
                _provider.FanController.MinSpeedPercentage,
                _provider.FanController.MaxSpeedPercentage,
            },
            CoreTempsSupported = _provider.TemperatureMonitor.SupportsCoreTemperatures,
            PowerSupported = _provider.PowerMonitor.IsSupported
        });
    }

    /// <summary>
    /// Metrics history for graphing. Backed by SQLite when available
    /// (queryable range), otherwise the in-memory rolling buffer.
    /// </summary>
    /// <param name="hours">Lookback window in hours (1-720)</param>
    /// <param name="limit">Max points returned (10-100000)</param>
    /// <param name="source">auto (default), db, or memory</param>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int hours = 1,
        [FromQuery] int limit = 2000,
        [FromQuery] string source = "auto",
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, 720);
        limit = Math.Clamp(limit, 10, 100000);

        if (!string.Equals(source, "memory", StringComparison.OrdinalIgnoreCase)
            && _history.IsAvailable)
        {
            var to = DateTime.UtcNow;
            var points = await _history.GetMetricHistoryAsync(
                to.AddHours(-hours), to, limit, cancellationToken);
            if (points.Count > 0 || string.Equals(source, "db", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(points.Select(p => new
                {
                    timestamp = p.Timestamp,
                    maxTemp = p.MaxTemp,
                    avgRpm = p.AvgRpm,
                    powerWatts = p.PowerWatts,
                    health = p.Health
                }));
            }
        }

        var history = _metrics.History;
        return Ok(history.Select(m => new
        {
            timestamp = m.Timestamp,
            maxTemp = m.MaxTemperature,
            avgRpm = m.AverageRpm,
            powerWatts = m.Power?.TotalWatts ?? 0,
            health = m.OverallHealth.ToString()
        }));
    }

    /// <summary>Metrics history as CSV export (SQLite-backed).</summary>
    [HttpGet("history/export")]
    public async Task<IActionResult> ExportHistory(
        [FromQuery] int hours = 24,
        [FromQuery] int limit = 20000,
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, 720);
        limit = Math.Clamp(limit, 10, 100000);

        var to = DateTime.UtcNow;
        var points = _history.IsAvailable
            ? await _history.GetMetricHistoryAsync(to.AddHours(-hours), to, limit, cancellationToken)
            : Array.Empty<MetricHistoryPoint>();

        var sb = new StringBuilder("timestamp,max_temp_c,avg_rpm,power_watts,health\n");
        foreach (var p in points)
            sb.Append(p.Timestamp.ToString("O")).Append(',')
              .Append(p.MaxTemp.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(p.AvgRpm).Append(',')
              .Append(p.PowerWatts.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(p.Health).Append('\n');

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "metrics-history.csv");
    }

    /// <summary>Set the manual fan speed</summary>
    [HttpPost("fans/speed/{percentage:int}")]
    public async Task<IActionResult> SetFanSpeed(int percentage, CancellationToken cancellationToken)
    {
        if (!_provider.FanController.SupportsManualControl)
            return BadRequest(new { error = "Manual fan control not supported" });

        var success = await _provider.FanController.SetFanSpeedAsync(percentage, cancellationToken);
        return success
            ? Ok(new { message = $"Fan speed set to {percentage}%" })
            : StatusCode(500, new { error = "Failed to set fan speed" });
    }

    /// <summary>Restore automatic fan control</summary>
    [HttpPost("fans/auto")]
    public async Task<IActionResult> RestoreAuto(CancellationToken cancellationToken)
    {
        var success = await _provider.FanController.RestoreAutoControlAsync(cancellationToken);
        return success
            ? Ok(new { message = "Restored automatic fan control" })
            : StatusCode(500, new { error = "Failed to restore auto fan control" });
    }

    /// <summary>Test the IPMI / hardware connection</summary>
    [HttpGet("test")]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        var ok = await _provider.TestConnectionAsync(cancellationToken);
        return Ok(new { connected = ok, provider = _provider.DisplayName });
    }

    // ------------------------------ System stats ------------------------------

    /// <summary>Latest system stats snapshot (CPU/memory/network/storage)</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<SystemStats>> GetStats(CancellationToken cancellationToken)
    {
        var snap = _stats.Latest ?? await _stats.RefreshAsync(cancellationToken);
        return Ok(snap);
    }

    /// <summary>
    /// Stats history for graphing (compact, time-series only).
    /// Backed by SQLite when available, otherwise the in-memory buffer.
    /// </summary>
    [HttpGet("stats/history")]
    public async Task<IActionResult> GetStatsHistory(
        [FromQuery] int hours = 1,
        [FromQuery] int limit = 2000,
        [FromQuery] string source = "auto",
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, 720);
        limit = Math.Clamp(limit, 10, 100000);

        if (!string.Equals(source, "memory", StringComparison.OrdinalIgnoreCase)
            && _history.IsAvailable)
        {
            var to = DateTime.UtcNow;
            var points = await _history.GetStatHistoryAsync(
                to.AddHours(-hours), to, limit, cancellationToken);
            if (points.Count > 0 || string.Equals(source, "db", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(points.Select(p => new
                {
                    timestamp = p.Timestamp,
                    cpuUsage = p.CpuUsage,
                    memUsage = p.MemUsage,
                    netRxTotal = p.NetRx,
                    netTxTotal = p.NetTx,
                    diskReadTotal = p.DiskRead,
                    diskWriteTotal = p.DiskWrite,
                }));
            }
        }

        var hist = _stats.History;
        return Ok(hist.Select(s => new
        {
            timestamp = s.Timestamp,
            cpuUsage = s.Cpu.UsagePercent,
            memUsage = s.Memory.UsagePercent,
            netRxTotal = s.NetworkInterfaces.Sum(i => i.ReceiveBytesPerSec),
            netTxTotal = s.NetworkInterfaces.Sum(i => i.TransmitBytesPerSec),
            diskReadTotal = s.StorageDevices.Sum(d => d.ReadBytesPerSec),
            diskWriteTotal = s.StorageDevices.Sum(d => d.WriteBytesPerSec),
        }));
    }

    /// <summary>System stats history as CSV export (SQLite-backed).</summary>
    [HttpGet("stats/export")]
    public async Task<IActionResult> ExportStats(
        [FromQuery] int hours = 24,
        [FromQuery] int limit = 20000,
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, 720);
        limit = Math.Clamp(limit, 10, 100000);

        var to = DateTime.UtcNow;
        var points = _history.IsAvailable
            ? await _history.GetStatHistoryAsync(to.AddHours(-hours), to, limit, cancellationToken)
            : Array.Empty<StatHistoryPoint>();

        var sb = new StringBuilder("timestamp,cpu_pct,mem_pct,net_rx_bps,net_tx_bps,disk_read_bps,disk_write_bps\n");
        foreach (var p in points)
            sb.Append(p.Timestamp.ToString("O")).Append(',')
              .Append(p.CpuUsage.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(p.MemUsage.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(p.NetRx.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(p.NetTx.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(p.DiskRead.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(p.DiskWrite.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "stats-history.csv");
    }

    /// <summary>Storage diagnostics: backend availability and row counts.</summary>
    [HttpGet("storage")]
    public async Task<IActionResult> GetStorage(CancellationToken cancellationToken)
    {
        var (metrics, stats) = await _history.GetCountsAsync(cancellationToken);
        return Ok(new { available = _history.IsAvailable, backend = "sqlite", metricRows = metrics, statRows = stats });
    }
}
