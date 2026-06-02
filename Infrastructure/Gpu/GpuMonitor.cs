using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;
using ServerMonitor.Infrastructure.System;

namespace ServerMonitor.Infrastructure.Gpu;

/// <summary>
/// GPU monitoring implementation supporting NVIDIA, AMD, and Intel GPUs
/// </summary>
public class GpuMonitor : IGpuMonitor
{
    private readonly ILogger<GpuMonitor> _logger;
    private readonly ShellExecutor _shell;

    public GpuMonitor(ILogger<GpuMonitor> logger, ShellExecutor shell)
    {
        _logger = logger;
        _shell = shell;
    }

    public async Task<List<GpuReading>> GetGpuReadingsAsync(CancellationToken cancellationToken = default)
    {
        var readings = new List<GpuReading>();

        // Try NVIDIA GPUs
        readings.AddRange(await GetNvidiaGpusAsync(cancellationToken));
        
        // Try AMD GPUs
        readings.AddRange(await GetAmdGpusAsync(cancellationToken));
        
        // Try Intel GPUs
        readings.AddRange(await GetIntelGpusAsync(cancellationToken));

        return readings;
    }

    private async Task<List<GpuReading>> GetNvidiaGpusAsync(CancellationToken cancellationToken)
    {
        var readings = new List<GpuReading>();

        try
        {
            if (!await _shell.IsCommandAvailableAsync("nvidia-smi"))
                return readings;

            var result = await _shell.ExecuteAsync(
                "nvidia-smi --query-gpu=name,temperature.gpu,fan.speed,power.draw,utilization.gpu --format=csv,noheader,nounits",
                cancellationToken: cancellationToken);

            if (!result.Success) return readings;

            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2) continue;

                var reading = new GpuReading
                {
                    Name = parts[0],
                    Vendor = GpuVendor.Nvidia,
                };

                if (parts.Length > 1 && double.TryParse(parts[1], out var temp))
                    reading.Temperature = temp;
                if (parts.Length > 2 && int.TryParse(parts[2], out var fan))
                    reading.FanSpeedPercent = fan;
                if (parts.Length > 3 && double.TryParse(parts[3], out var power))
                    reading.PowerWatts = power;
                if (parts.Length > 4 && int.TryParse(parts[4], out var usage))
                    reading.UsagePercent = usage;

                readings.Add(reading);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query NVIDIA GPUs");
        }

        return readings;
    }

    private async Task<List<GpuReading>> GetAmdGpusAsync(CancellationToken cancellationToken)
    {
        var readings = new List<GpuReading>();

        try
        {
            if (!await _shell.IsCommandAvailableAsync("rocm-smi"))
                return readings;

            var result = await _shell.ExecuteAsync(
                "rocm-smi --showtemp --showfan --csv 2>/dev/null",
                cancellationToken: cancellationToken);

            if (!result.Success) return readings;

            // Parse CSV - rocm-smi output varies
            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2) continue;

                var reading = new GpuReading
                {
                    Name = $"AMD GPU {parts[0]}",
                    Vendor = GpuVendor.Amd
                };

                if (parts.Length > 1 && double.TryParse(parts[1], out var temp))
                    reading.Temperature = temp;
                if (parts.Length > 2 && int.TryParse(parts[2], out var fan))
                    reading.FanSpeedPercent = fan;

                readings.Add(reading);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query AMD GPUs");
        }

        return readings;
    }

    private async Task<List<GpuReading>> GetIntelGpusAsync(CancellationToken cancellationToken)
    {
        var readings = new List<GpuReading>();

        try
        {
            // Read Intel GPU temperature from sysfs
            var result = await _shell.ExecuteAsync(
                "for f in /sys/class/drm/card*/device/hwmon/hwmon*/temp1_input 2>/dev/null; do " +
                "[ -f \"$f\" ] && echo \"$f:$(cat $f)\"; done",
                cancellationToken: cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
                return readings;

            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int gpuIndex = 0;

            foreach (var line in lines)
            {
                var parts = line.Split(':');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[1].Trim(), out var rawTemp)) continue;

                readings.Add(new GpuReading
                {
                    Name = $"Intel GPU {gpuIndex}",
                    Vendor = GpuVendor.Intel,
                    Temperature = rawTemp / 1000.0  // sysfs gives millicelsius
                });
                gpuIndex++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query Intel GPUs");
        }

        return readings;
    }
}
