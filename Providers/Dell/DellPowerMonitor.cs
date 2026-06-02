using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Providers.Dell;

/// <summary>
/// Dell power monitor implementation
/// Reads "Pwr Consumption" or similar sensor from IPMI
/// </summary>
public class DellPowerMonitor : IPowerMonitor
{
    private readonly IIpmiClient _ipmi;
    private readonly ILogger<DellPowerMonitor> _logger;

    public bool IsSupported => true;

    public DellPowerMonitor(IIpmiClient ipmi, ILogger<DellPowerMonitor> logger)
    {
        _ipmi = ipmi;
        _logger = logger;
    }

    public async Task<PowerReading> GetPowerMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sensors = await _ipmi.GetAllSensorsAsync(cancellationToken);

            // Try to find power consumption sensor by known names
            foreach (var name in DellCommands.PowerSensorNames)
            {
                var sensor = sensors.FirstOrDefault(s =>
                    string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    s.Unit.ToLowerInvariant().Contains("watt"));

                if (sensor != null)
                {
                    return new PowerReading
                    {
                        TotalWatts = sensor.Value,
                        Source = "IPMI:" + sensor.Name,
                        IsAvailable = true,
                        Timestamp = sensor.Timestamp
                    };
                }
            }

            // Fallback: any watt sensor
            var anyWatts = sensors.FirstOrDefault(s =>
                s.Unit.ToLowerInvariant().Contains("watt") && s.Value > 0);
            if (anyWatts != null)
            {
                return new PowerReading
                {
                    TotalWatts = anyWatts.Value,
                    Source = "IPMI:" + anyWatts.Name,
                    IsAvailable = true,
                    Timestamp = anyWatts.Timestamp
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read power consumption");
        }

        return new PowerReading
        {
            TotalWatts = 0,
            Source = "Unavailable",
            IsAvailable = false
        };
    }
}
