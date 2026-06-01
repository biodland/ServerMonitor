using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DellFanControl.Services;

public class IPMIService : IDisposable
{
    private readonly ILogger<IPMIService> _logger;
    private readonly IConfiguration _configuration;

    public IPMIService(ILogger<IPMIService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private string IDRAC_IP => _configuration["Idrac:Ip"] ?? "192.168.0.101";
    private string IDRAC_USER => _configuration["Idrac:User"] ?? "root";
    private string IDRAC_PASS => _configuration["Idrac:Password"] ?? string.Empty;

    /// <summary>
    /// Gets current temperature readings from iDRAC
    /// </summary>
    public async Task<TemperatureStatus> GetTemperaturesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Reading temperatures from iDRAC...");
            
            // Execute ipmitool command to get temperature readings
            var result = await RunIpmitoolCommandAsync("sdr type temperature", cancellationToken);
            
            // Parse the output similar to the bash script
            var temps = ParseTemperatureReadings(result);
            
            var highestTemp = temps.Count > 0 ? temps.Max() : 0;
            
            _logger.LogInformation("Highest temperature: {HighestTemp}°C (Total sensors: {SensorCount})", 
                highestTemp, temps.Count);
            
            return new TemperatureStatus
            {
                HighestTempCelsius = highestTemp,
                AllTemperatures = temps,
                Timestamp = DateTime.UtcNow,
                LastUpdateTime = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read temperatures from iDRAC");
            throw;
        }
    }

    /// <summary>
    /// Sets fan speed to a specific percentage
    /// </summary>
    public async Task SetFanSpeedAsync(int percentage, CancellationToken cancellationToken = default)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Fan speed must be between 0 and 100");
        }

        try
        {
            // Convert percentage to hex value (similar to bash script)
            string hexValue = "0x" + (percentage / 10).ToString("X2");
            
            _logger.LogInformation("Setting fan speed to {Percentage}% (Hex: {HexValue})", percentage, hexValue);
            
            // Execute the IPMI command to set fan speed
            // raw 0x30 0x30 0x02 0xff <hex_value>
            string command = $"raw 0x30 0x30 0x02 0xff {hexValue}";
            await RunIpmitoolCommandAsync(command, cancellationToken);
            
            _logger.LogInformation("Fan speed set successfully to {Percentage}%", percentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set fan speed to {Percentage}%", percentage);
            throw;
        }
    }

    /// <summary>
    /// Restores dynamic fan control (automatic fan control by iDRAC)
    /// </summary>
    public async Task RestoreDynamicControlAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Restoring dynamic fan control...");
            
            // raw 0x30 0x30 0x01 0x01
            await RunIpmitoolCommandAsync("raw 0x30 0x30 0x01 0x01", cancellationToken);
            
            _logger.LogInformation("Dynamic fan control restored successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore dynamic fan control");
            throw;
        }
    }

    /// <summary>
    /// Gets current fan speed status
    /// </summary>
    public async Task<FanStatus> GetFanStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunIpmitoolCommandAsync("sdr type fan", cancellationToken);
            
            var fans = ParseFanReadings(result);
            
            _logger.LogInformation("Fan status retrieved: {FanCount} fans detected", fans.Count);
            
            return new FanStatus
            {
                Fans = fans,
                Timestamp = DateTime.UtcNow,
                LastUpdateTime = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get fan status from iDRAC");
            throw;
        }
    }

    /// <summary>
    /// Runs an ipmitool command and returns the output
    /// </summary>
    private async Task<string> RunIpmitoolCommandAsync(string command, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ipmitool",
            Arguments = $"-I lanplus -H {IDRAC_IP} -U {IDRAC_USER} -P {IDRAC_PASS} {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        _logger.LogDebug("Running ipmitool: ipmitool {Arguments}", startInfo.Arguments);

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            _logger.LogError("ipmitool command failed with exit code {ExitCode}: {Error}", 
                process.ExitCode, error);
            throw new InvalidOperationException($"ipmitool command failed: {error}");
        }

        return outputBuilder.ToString();
    }

    /// <summary>
    /// Parses temperature readings from ipmitool output
    /// </summary>
    private List<int> ParseTemperatureReadings(string output)
    {
        var temps = new List<int>();
        
        // Parse output like: "Ambient Temp     | 22 degrees C | ok"
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            // Match temperature values (looking for patterns like "| 22 C" or "| 22 degrees C")
            var match = System.Text.RegularExpressions.Regex.Match(
                line, 
                @"\|\s*(\d+)\s*(degrees)?\s*C"
            );
            
            if (match.Success && int.TryParse(match.Groups[1].Value, out int temp))
            {
                temps.Add(temp);
            }
        }
        
        return temps;
    }

    /// <summary>
    /// Parses fan readings from ipmitool output
    /// </summary>
    private List<FanInfo> ParseFanReadings(string output)
    {
        var fans = new List<FanInfo>();
        
        // Parse output like: "Fan1 RPM        | 4800 RPM | ok"
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                line, 
                @"^(.*?)\s*\|\s*(\d+)\s*RPM"
            );
            
            if (match.Success)
            {
                var fanName = match.Groups[1].Value.Trim();
                if (int.TryParse(match.Groups[2].Value, out int rpm))
                {
                    fans.Add(new FanInfo
                    {
                        Name = fanName,
                        RPM = rpm,
                        Timestamp = DateTime.Now
                    });
                }
            }
        }
        
        return fans;
    }

    public void Dispose()
    {
        //.Dispose is handled by using statements in RunIpmitoolCommandAsync
    }
}

/// <summary>
/// Temperature status information
/// </summary>
public class TemperatureStatus
{
    public int HighestTempCelsius { get; set; }
    public List<int> AllTemperatures { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public DateTime LastUpdateTime { get; set; }
}

/// <summary>
/// Fan status information
/// </summary>
public class FanStatus
{
    public List<FanInfo> Fans { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public int AverageRPM => Fans.Count > 0 ? (int)Fans.Average(f => f.RPM) : 0;
}

/// <summary>
/// Individual fan information
/// </summary>
public class FanInfo
{
    public string Name { get; set; } = string.Empty;
    public int RPM { get; set; }
    public DateTime Timestamp { get; set; }
}