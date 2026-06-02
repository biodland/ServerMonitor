using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DellFanControl.Services;

/// <summary>
/// Service for detecting and reading GPU temperatures (NVIDIA, AMD, etc.)
/// </summary>
public class GpuService
{
    private readonly ILogger<GpuService> _logger;

    public GpuService(ILogger<GpuService> logger)
    {
        _logger = logger;
        }

    /// <summary>
    /// Get all GPU temperatures available on the system
    /// </summary>
    public async Task<List<GpuReading>> GetGpuTemperaturesAsync()
    {
        var gpuTemps = new List<GpuReading>();

        try
        {
            // Try NVIDIA GPUs first (nvidia-smi)
            var nvidiaTemps = await GetNvidiaGpuTemperaturesAsync();
            gpuTemps.AddRange(nvidiaTemps);

            // Try AMD GPUs (rocm-smi)
            var amdTemps = await GetAmdGpuTemperaturesAsync();
            gpuTemps.AddRange(amdTemps);

            // Try Intel GPUs (intel_gpu_top or others)
            var intelTemps = await GetIntelGpuTemperaturesAsync();
            gpuTemps.AddRange(intelTemps);

            _logger.LogDebug("Retrieved {Count} GPU temperature readings", gpuTemps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error retrieving GPU temperatures");
        }

        return gpuTemps;
    }

    /// <summary>
    /// Get NVIDIA GPU temperatures using nvidia-smi
    /// </summary>
    private async Task<List<GpuReading>> GetNvidiaGpuTemperaturesAsync()
    {
        var temps = new List<GpuReading>();

        try
        {
            var command = "nvidia-smi --query-gpu=index,name,temperature.gpu,fan.speed --format=csv,noheader,nounits";
            var output = await ExecuteCommandAsync(command);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Parse nvidia-smi output: 0, GeForce RTX 2080 Ti, 45, 65
                var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 3)
                {
                    if (int.TryParse(parts[0], out var gpuIndex) &&
                        double.TryParse(parts[2], out var temp))
                    {
                        var gpuName = parts.Length >= 2 ? parts[1] : $"GPU {gpuIndex}";
                        
                        temps.Add(new GpuReading
                        {
                            Name = gpuName,
                            Type = "NVIDIA",
                            Temperature = temp,
                            FanSpeed = parts.Length >= 4 && double.TryParse(parts[3], out var fan) ? fan : null
                        });
                    }
                }
            }

            _logger.LogDebug("Found {Count} NVIDIA GPUs", temps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("No NVIDIA GPUs detected or nvidia-smi not available: {Message}", ex.Message);
        }

        return temps;
    }

    /// <summary>
    /// Get AMD GPU temperatures using rocm-smi
    /// </summary>
    private async Task<List<GpuReading>> GetAmdGpuTemperaturesAsync()
    {
        var temps = new List<GpuReading>();

        try
        {
            // Check if rocm-smi is available
            var checkCommand = "which rocm-smi";
            await ExecuteCommandAsync(checkCommand);

            var command = "rocm-smi --showtemp --showpower --showfan --csv";
            var output = await ExecuteCommandAsync(command);

            // Parse AMD ROCm output
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                var parts = lines[i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 && double.TryParse(parts[1], out var temp))
                {
                    temps.Add(new GpuReading
                    {
                        Name = $"AMD GPU {i}",
                        Type = "AMD",
                        Temperature = temp
                    });
                }
            }

            _logger.LogDebug("Found {Count} AMD GPUs", temps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("No AMD GPUs detected or rocm-smi not available: {Message}", ex.Message);
        }

        return temps;
    }

    /// <summary>
    /// Get Intel GPU temperatures
    /// </summary>
    private async Task<List<GpuReading>> GetIntelGpuTemperaturesAsync()
    {
        var temps = new List<GpuReading>();

        try
        {
            // Try intel_gpu_top or /sys/class/drm
            var command = "cat /sys/class/drm/card*/device/hwmon/hwmon*/temp1_input 2>/dev/null | grep -v '^$'";
            var output = await ExecuteCommandAsync(command);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int cardIndex = 0;

            foreach (var line in lines)
            {
                if (int.TryParse(line.Trim(), out var tempRaw))
                {
                    // Intel GPU temps are in millidegrees Celsius
                    var temp = tempRaw / 1000.0;
                    
                    temps.Add(new GpuReading
                    {
                        Name = $"Intel GPU {cardIndex}",
                        Type = "Intel",
                        Temperature = temp
                    });
                    cardIndex++;
                }
            }

            _logger.LogDebug("Found {Count} Intel GPUs", temps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("No Intel GPUs detected: {Message}", ex.Message);
        }

        return temps;
    }

    /// <summary>
    /// Execute shell command asynchronously
    /// </summary>
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
                    // Command failed, return empty string
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
            tcs.SetResult(string.Empty);
        }

        return tcs.Task;
    }
}

public record GpuReading
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public double Temperature { get; init; }
    public double? FanSpeed { get; init; }
}