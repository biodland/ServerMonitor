using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Infrastructure.System;

/// <summary>
/// Parses output from the lm-sensors `sensors` command
/// to extract CPU core temperatures
/// </summary>
public class LmSensorsParser
{
    private readonly ILogger<LmSensorsParser> _logger;
    private readonly ShellExecutor _shell;
    
    private static readonly Regex PackageRegex = 
        new(@"Package id (\d+):\s+\+?(-?\d+\.?\d*)", RegexOptions.Compiled);
    private static readonly Regex CoreRegex = 
        new(@"Core (\d+):\s+\+?(-?\d+\.?\d*)", RegexOptions.Compiled);
    private static readonly Regex ThresholdRegex =
        new(@"high\s*=\s*\+?(-?\d+\.?\d*).*crit\s*=\s*\+?(-?\d+\.?\d*)", RegexOptions.Compiled);

    public LmSensorsParser(ILogger<LmSensorsParser> logger, ShellExecutor shell)
    {
        _logger = logger;
        _shell = shell;
    }

    /// <summary>
    /// Check if lm-sensors is available
    /// </summary>
    public Task<bool> IsAvailableAsync()
    {
        return _shell.IsCommandAvailableAsync("sensors");
    }

    /// <summary>
    /// Get all CPU core temperatures via lm-sensors
    /// </summary>
    public async Task<List<TemperatureReading>> GetCpuCoreTemperaturesAsync(
        CancellationToken cancellationToken = default)
    {
        var cores = new List<TemperatureReading>();

        try
        {
            var result = await _shell.ExecuteAsync("sensors", cancellationToken: cancellationToken);
            if (!result.Success)
            {
                _logger.LogDebug("sensors command failed");
                return cores;
            }

            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? currentPackage = null;

            foreach (var line in lines)
            {
                var packageMatch = PackageRegex.Match(line);
                if (packageMatch.Success)
                {
                    currentPackage = packageMatch.Groups[1].Value;
                    var thresholds = ExtractThresholds(line);
                    
                    if (double.TryParse(packageMatch.Groups[2].Value, 
                        global::System.Globalization.NumberStyles.Float,
                        global::System.Globalization.CultureInfo.InvariantCulture,
                        out var temp))
                    {
                        cores.Add(new TemperatureReading
                        {
                            Name = $"CPU {currentPackage} Package",
                            Value = temp,
                            Unit = "°C",
                            HighThreshold = thresholds.High,
                            CriticalThreshold = thresholds.Critical,
                            Source = TemperatureSource.LmSensors,
                            IsPackage = true
                        });
                    }
                    continue;
                }

                var coreMatch = CoreRegex.Match(line);
                if (coreMatch.Success && currentPackage != null)
                {
                    var thresholds = ExtractThresholds(line);
                    
                    if (double.TryParse(coreMatch.Groups[2].Value,
                        global::System.Globalization.NumberStyles.Float,
                        global::System.Globalization.CultureInfo.InvariantCulture,
                        out var temp))
                    {
                        cores.Add(new TemperatureReading
                        {
                            Name = $"CPU {currentPackage} Core {coreMatch.Groups[1].Value}",
                            Value = temp,
                            Unit = "°C",
                            HighThreshold = thresholds.High,
                            CriticalThreshold = thresholds.Critical,
                            Source = TemperatureSource.LmSensors,
                            IsPackage = false
                        });
                    }
                }
            }

            _logger.LogDebug("Retrieved {Count} CPU core temperatures", cores.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing lm-sensors output");
        }

        return cores;
    }

    private (double? High, double? Critical) ExtractThresholds(string line)
    {
        var match = ThresholdRegex.Match(line);
        if (!match.Success) return (null, null);

        var culture = global::System.Globalization.CultureInfo.InvariantCulture;
        double? high = double.TryParse(match.Groups[1].Value, 
            global::System.Globalization.NumberStyles.Float, culture, out var h) ? h : null;
        double? crit = double.TryParse(match.Groups[2].Value,
            global::System.Globalization.NumberStyles.Float, culture, out var c) ? c : null;
        
        return (high, crit);
    }
}
