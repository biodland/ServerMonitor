using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Providers.Dell;

/// <summary>
/// Dell fan controller implementation
/// Uses Dell-specific IPMI raw OEM commands to control fan speeds
/// </summary>
public class DellFanController : IFanController
{
    private readonly IIpmiClient _ipmi;
    private readonly ILogger<DellFanController> _logger;

    public bool SupportsManualControl => true;
    public int MinSpeedPercentage => 5;
    public int MaxSpeedPercentage => 100;

    public DellFanController(IIpmiClient ipmi, ILogger<DellFanController> logger)
    {
        _ipmi = ipmi;
        _logger = logger;
    }

    public async Task<List<FanReading>> GetFanStatusAsync(CancellationToken cancellationToken = default)
    {
        var readings = new List<FanReading>();
        try
        {
            var sensors = await _ipmi.GetAllSensorsAsync(cancellationToken);
            foreach (var s in sensors)
            {
                if (!IsFanSensor(s)) continue;

                readings.Add(new FanReading
                {
                    Name = s.Name,
                    RPM = (int)s.Value,
                    IsHealthy = s.Status == "ok" || string.IsNullOrEmpty(s.Status),
                    Timestamp = s.Timestamp
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read fan status");
        }

        return readings;
    }

    public async Task<bool> SetFanSpeedAsync(int percentage, CancellationToken cancellationToken = default)
    {
        if (percentage < MinSpeedPercentage || percentage > MaxSpeedPercentage)
        {
            _logger.LogWarning("Fan speed {Percentage}% out of allowed range [{Min}-{Max}]",
                percentage, MinSpeedPercentage, MaxSpeedPercentage);
            return false;
        }

        try
        {
            // Step 1: Enable manual control
            await _ipmi.ExecuteRawAsync(DellCommands.EnableManualControl, cancellationToken);

            // Step 2: Set the desired speed
            var cmd = DellCommands.BuildSetFanSpeedCommand(percentage);
            await _ipmi.ExecuteRawAsync(cmd, cancellationToken);

            _logger.LogInformation("Fan speed set to {Percentage}% (manual mode)", percentage);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set fan speed to {Percentage}%", percentage);
            return false;
        }
    }

    public async Task<bool> RestoreAutoControlAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _ipmi.ExecuteRawAsync(DellCommands.EnableAutoControl, cancellationToken);
            _logger.LogInformation("Restored automatic fan control");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore automatic fan control");
            return false;
        }
    }

    private static bool IsFanSensor(SensorReading s)
    {
        if (string.IsNullOrEmpty(s.Unit)) return false;
        var unit = s.Unit.ToLowerInvariant();
        if (!unit.Contains("rpm")) return false;
        if (s.Status == "ns" || s.Status == "na") return false;
        return true;
    }
}
