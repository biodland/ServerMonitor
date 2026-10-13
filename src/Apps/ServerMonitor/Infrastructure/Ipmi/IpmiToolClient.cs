using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;
using ServerMonitor.Infrastructure.System;

namespace ServerMonitor.Infrastructure.Ipmi;

/// <summary>
/// IPMI client implementation using the ipmitool command-line utility
/// Supports both in-band (local) and out-of-band (network) IPMI
/// </summary>
public class IpmiToolClient : IIpmiClient
{
    private readonly ILogger<IpmiToolClient> _logger;
    private readonly ShellExecutor _shell;
    private readonly IpmiConfiguration _config;
    private bool? _availabilityCache;

    // The BMC / ipmitool handles concurrent requests poorly (slow serial
    // responses, timeouts under contention), so all ipmitool invocations are
    // serialized through this gate.
    private readonly SemaphoreSlim _ipmiGate = new(1, 1);

    // `ipmitool sensor` is slow on older BMCs (5-15s on a Dell R720) and is
    // requested 3x per metrics cycle (temperatures + fans + power). Cache a
    // single result briefly so one cycle costs one BMC round-trip.
    private readonly object _sensorCacheLock = new();
    private List<SensorReading>? _sensorCache;
    private DateTime _sensorCacheTime = DateTime.MinValue;
    private static readonly TimeSpan SensorCacheTtl = TimeSpan.FromSeconds(4);

    // `mc info` barely changes - cache for minutes. This also keeps the
    // per-cycle GetServerInfoAsync call (MetricsCollectorService) cheap.
    private readonly object _bmcCacheLock = new();
    private Dictionary<string, string>? _bmcCache;
    private DateTime _bmcCacheTime = DateTime.MinValue;
    private static readonly TimeSpan BmcCacheTtl = TimeSpan.FromMinutes(5);

    // `which ipmitool` result cache - avoids a shell spawn on every check.
    private bool _toolChecked;
    private bool _toolAvailable;
    private DateTime _toolCheckTime = DateTime.MinValue;
    private static readonly TimeSpan ToolCheckTtl = TimeSpan.FromMinutes(5);

    private const int SensorTimeoutMs = 15000;
    private const int DefaultIpmiTimeoutMs = 10000;

    public IpmiClientType ClientType { get; }

    public bool IsAvailable => _availabilityCache ?? false;

    public IpmiToolClient(
        ILogger<IpmiToolClient> logger,
        ShellExecutor shell,
        IConfiguration configuration)
    {
        _logger = logger;
        _shell = shell;
        _config = LoadConfiguration(configuration);
        ClientType = string.IsNullOrEmpty(_config.Host) || _config.Host == "127.0.0.1"
            ? IpmiClientType.InBand
            : IpmiClientType.OutBand;
    }

    private IpmiConfiguration LoadConfiguration(IConfiguration configuration)
    {
        // Try new configuration path first, fall back to old one for backward compatibility
        var section = configuration.GetSection("ServerMonitor:Ipmi");
        if (!section.Exists())
        {
            section = configuration.GetSection("Idrac");
        }

        return new IpmiConfiguration
        {
            Host = section["Host"] ?? section["Ip"] ?? "127.0.0.1",
            Username = section["Username"] ?? section["User"] ?? string.Empty,
            Password = section["Password"] ?? string.Empty,
            Interface = section["Interface"] ?? "lanplus",
            CipherSuite = int.TryParse(section["CipherSuite"], out var cs) ? cs : 3,
            UseLocal = bool.TryParse(section["UseLocal"], out var ul) ? ul : true
        };
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // First check if ipmitool is installed (cached - avoids a shell spawn per call)
            var available = await IsToolAvailableAsync(cancellationToken);
            if (!available)
            {
                _logger.LogError("ipmitool is not installed");
                _availabilityCache = false;
                return false;
            }

            var result = await ExecuteCommandAsync("mc info", cancellationToken);
            var success = result.Contains("Device ID") || result.Contains("Manufacturer");
            _availabilityCache = success;

            if (success)
                _logger.LogInformation("IPMI connection test successful ({Type})", ClientType);
            else
                _logger.LogWarning("IPMI connection test failed");

            return success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IPMI connection test error");
            _availabilityCache = false;
            return false;
        }
    }

    private async Task<bool> IsToolAvailableAsync(CancellationToken cancellationToken)
    {
        lock (_sensorCacheLock)
        {
            if (_toolChecked && DateTime.UtcNow - _toolCheckTime < ToolCheckTtl)
                return _toolAvailable;
        }

        var available = await _shell.IsCommandAvailableAsync("ipmitool");
        lock (_sensorCacheLock)
        {
            _toolChecked = true;
            _toolAvailable = available;
            _toolCheckTime = DateTime.UtcNow;
        }
        return available;
    }

    public async Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        var fullCommand = BuildCommand(command);
        _logger.LogDebug("Executing IPMI: {Command}", command);

        await _ipmiGate.WaitAsync(cancellationToken);
        try
        {
            var result = await _shell.ExecuteAsync(
                fullCommand, timeoutMs: DefaultIpmiTimeoutMs, cancellationToken: cancellationToken);

            if (!result.Success && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("IPMI command failed: {Command} - {Error}",
                    command, result.StandardError);
            }

            return result.StandardOutput;
        }
        finally
        {
            _ipmiGate.Release();
        }
    }

    public async Task<string> ExecuteRawAsync(string rawHex, CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync($"raw {rawHex}", cancellationToken);
    }

    public async Task<List<SensorReading>> GetAllSensorsAsync(CancellationToken cancellationToken = default)
    {
        lock (_sensorCacheLock)
        {
            if (_sensorCache != null && DateTime.UtcNow - _sensorCacheTime < SensorCacheTtl)
                return new List<SensorReading>(_sensorCache);
        }

        await _ipmiGate.WaitAsync(cancellationToken);
        try
        {
            // Double-checked: a concurrent caller may have refreshed while we waited.
            lock (_sensorCacheLock)
            {
                if (_sensorCache != null && DateTime.UtcNow - _sensorCacheTime < SensorCacheTtl)
                    return new List<SensorReading>(_sensorCache);
            }

            var readings = new List<SensorReading>();

            try
            {
                var fullCommand = BuildCommand("sensor");
                var result = await _shell.ExecuteAsync(
                    fullCommand, timeoutMs: SensorTimeoutMs, cancellationToken: cancellationToken);

                if (!result.Success)
                {
                    if (!cancellationToken.IsCancellationRequested)
                        _logger.LogWarning("IPMI command failed: sensor - {Error}", result.StandardError);
                    return readings;
                }

                var output = result.StandardOutput;
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length < 3) continue;

                    var name = parts[0].Trim();
                    var value = parts[1].Trim();
                    var unit = parts[2].Trim();
                    var status = parts.Length > 3 ? parts[3].Trim() : "ok";

                    if (double.TryParse(value, out var numericValue))
                    {
                        readings.Add(new SensorReading
                        {
                            Name = name,
                            Value = numericValue,
                            Unit = unit,
                            Status = status
                        });
                    }
                }

                lock (_sensorCacheLock)
                {
                    _sensorCache = readings;
                    _sensorCacheTime = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve sensors");
            }

            return readings;
        }
        finally
        {
            _ipmiGate.Release();
        }
    }

    public async Task<Dictionary<string, string>> GetBmcInfoAsync(CancellationToken cancellationToken = default)
    {
        lock (_bmcCacheLock)
        {
            if (_bmcCache != null && DateTime.UtcNow - _bmcCacheTime < BmcCacheTtl)
                return new Dictionary<string, string>(_bmcCache);
        }

        var info = new Dictionary<string, string>();

        try
        {
            var output = await ExecuteCommandAsync("mc info", cancellationToken);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = line.Substring(0, colonIndex).Trim();
                    var value = line.Substring(colonIndex + 1).Trim();
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    {
                        info[key] = value;
                    }
                }
            }

            if (info.Count > 0)
            {
                lock (_bmcCacheLock)
                {
                    _bmcCache = info;
                    _bmcCacheTime = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve BMC info");
        }

        return info;
    }

    /// <summary>
    /// Build full ipmitool command with appropriate flags
    /// </summary>
    private string BuildCommand(string arguments)
    {
        var parts = new List<string> { "ipmitool" };

        // Use network IPMI if host is specified and not local
        if (!string.IsNullOrEmpty(_config.Host) && _config.Host != "127.0.0.1" && !_config.UseLocal)
        {
            parts.Add($"-I {_config.Interface}");
            parts.Add($"-H {_config.Host}");

            if (!string.IsNullOrEmpty(_config.Username))
                parts.Add($"-U {_config.Username}");

            if (!string.IsNullOrEmpty(_config.Password))
                parts.Add($"-P {_config.Password}");

            if (_config.CipherSuite > 0)
                parts.Add($"-C {_config.CipherSuite}");
        }

        parts.Add(arguments);
        return string.Join(" ", parts);
    }
}

/// <summary>
/// IPMI configuration loaded from appsettings
/// </summary>
internal class IpmiConfiguration
{
    public string Host { get; set; } = "127.0.0.1";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Interface { get; set; } = "lanplus";
    public int CipherSuite { get; set; } = 3;
    public bool UseLocal { get; set; } = true;
}
