using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DellFanControl.Services;

/// <summary>
/// Service for monitoring system metrics including CPU core temperatures and power consumption
/// </summary>
public class SystemMetricsService
{
    private readonly ILogger<SystemMetricsService> _logger;
    private readonly IIPMIService _ipmiService;

    public SystemMetricsService(ILogger<SystemMetricsService> logger, IIPMIService ipmiService)
    {
        _logger = logger;
        _ipmiService = ipmiService;
    }

    /// <summary>
    /// Get CPU core temperatures from lm-sensors
    /// </summary>
    public async Task<List<CpuCoreTemp>> GetCpuCoreTemperaturesAsync()
    {
        var cores = new List<CpuCoreTemp>();

        try
        {
            var output = await ExecuteCommandAsync("sensors");

            // Parse lm-sensors output for CPU cores
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? currentPackage = null;

            foreach (var line in lines)
            {
                // Match Package id: +XX.0°C
                var packageMatch = Regex.Match(line, @"Package id (\d+):\s+\+(\d+)\.\d+");
                if (packageMatch.Success)
                {
                    currentPackage = packageMatch.Groups[1].Value;
                    cores.Add(new CpuCoreTemp
                    {
                        Name = $"CPU {currentPackage} Package",
                        Temperature = int.Parse(packageMatch.Groups[2].Value),
                        IsPackage = true
                    });
                    continue;
                }

                // Match Core N: +XX.0°C
                var coreMatch = Regex.Match(line, @"Core (\d+):\s+\+(\d+)\.\d+");
                if (coreMatch.Success && currentPackage != null)
                {
                    cores.Add(new CpuCoreTemp
                    {
                        Name = $"CPU {currentPackage} Core {coreMatch.Groups[1].Value}",
                        Temperature = int.Parse(coreMatch.Groups[2].Value),
                        IsPackage = false
                    });
                }
            }

            _logger.LogDebug("Retrieved {Count} CPU core temperature readings", cores.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving CPU core temperatures");
        }

        return cores;
    }

    /// <summary>
    /// Get power consumption metrics from IPMI and system
    /// </summary>
    public async Task<PowerMetrics> GetPowerMetricsAsync()
    {
        var metrics = new PowerMetrics();

        try
        {
            // Get power from IPMI sensors
            var command = "ipmitool sensor | grep -i 'Watt\\|Power'";
            var output = await ExecuteCommandAsync(command);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    var sensorName = parts[0].Trim().ToLower();
                    var value = parts[1].Trim();

                    if (double.TryParse(value, out var power))
                    {
                        // Detect system power consumption
                        if (sensorName.Contains("system") || sensorName.Contains("pwr consumption"))
                        {
                            metrics.TotalWatts = power;
                            metrics.TotalSource = "IPMI";
                        }
                        // Detect CPU power
                        else if (sensorName.Contains("cpu"))
                        {
                            metrics.CpuWatts = power;
                        }
                        // Detect power supply readings
                        else if (sensorName.Contains("psu") || sensorName.Contains("power supply"))
                        {
                            metrics.PowerSupply = power;
                        }
                    }
                }
            }

            // Try to get power from RAPL if IPMI doesn't provide it
            if (metrics.TotalWatts == 0)
            {
                var raplOutput = await ExecuteCommandAsync("cat /sys/class/powercap/intel-rapl/intel-rapl:0/energy_uj 2>/dev/null || echo '0'");
                if (long.TryParse(raplOutput.Trim(), out var energy))
                {
                    // RAPL keeps a cumulative counter, we can calculate instantaneous power
                    // For simplicity, we'll use a proxy estimate based on temperature
                    var tempStatus = await _ipmiService.GetTemperaturesAsync();
                    metrics.TotalWatts = EstimatePowerFromTemps(tempStatus.AllTemperatures);
                    metrics.TotalSource = "Estimated";
                }
            }

            _logger.LogDebug("Retrieved power metrics: {Metrics}", metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving power metrics");
        }

        return metrics;
    }

    /// <summary>
    /// Estimate power consumption based on temperatures (fallback method)
    /// </summary>
    private double EstimatePowerFromTemps(List<int> temps)
    {
        if (temps.Count == 0) return 0;

        // Rough estimation: base 100W + 5W per degree above 50°C for each sensor
        var maxTemp = temps.Max();
        var excess = Math.Max(0, maxTemp - 50);
        return 100 + (excess * 5 * temps.Count * 0.2);
    }

    private Task<string> ExecuteCommandAsync(string command)
    {
        var tcs = new TaskCompletionSource<string>();

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    error.AppendLine(e.Data);
                }
            };

            process.Exited += (sender, e) =>
            {
                if (process.ExitCode == 0)
                {
                    tcs.SetResult(output.ToString());
                }
                else
                {
                    tcs.SetResult(string.Empty);
                }
                process.Dispose();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command execution failed");
            tcs.SetResult(string.Empty);
        }

        return tcs.Task;
    }
}

public record CpuCoreTemp
{
    public string Name { get; init; } = string.Empty;
    public int Temperature { get; init; }
    public bool IsPackage { get; init; }
}

public record PowerMetrics
{
    public double TotalWatts { get; init; }
    public string? TotalSource { get; init; }
    public double CpuWatts { get; init; }
    public double PowerSupply { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}