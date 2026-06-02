using Microsoft.AspNetCore.Mvc;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;
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
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        IMetricsCollectorService metrics,
        IServerProvider provider,
        ISystemStatsService stats,
        ILogger<ApiController> logger)
    {
        _metrics = metrics;
        _provider = provider;
        _stats = stats;
        _logger = logger;
    }

    /// <summary>Latest metrics snapshot</summary>
    [HttpGet("status")]
    public async Task<ActionResult<ServerMetrics>> GetStatus(CancellationToken cancellationToken)
    {
        var metrics = _metrics.Latest;
        if (metrics == null)
            metrics = await _metrics.RefreshAsync(cancellationToken);
        return Ok(metrics);
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

    /// <summary>Metrics history for graphing</summary>
    [HttpGet("history")]
    public IActionResult GetHistory()
    {
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

    /// <summary>Stats history for graphing (compact, time-series only)</summary>
    [HttpGet("stats/history")]
    public IActionResult GetStatsHistory()
    {
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
}
