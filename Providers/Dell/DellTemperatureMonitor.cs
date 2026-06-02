using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;
using ServerMonitor.Infrastructure.System;

namespace ServerMonitor.Providers.Dell;

/// <summary>
/// Dell temperature monitor implementation
/// Reads temperatures via IPMI sensors and (optionally) lm-sensors for CPU cores
/// </summary>
public class DellTemperatureMonitor : ITemperatureMonitor
{
    private readonly IIpmiClient _ipmi;
    private readonly LmSensorsParser _lmSensors;
    private readonly ILogger<DellTemperatureMonitor> _logger;
    private bool? _coreTempsAvailable;

    public bool SupportsCoreTemperatures => _coreTempsAvailable ?? true;

    public DellTemperatureMonitor(
        IIpmiClient ipmi,
        LmSensorsParser lmSensors,
        ILogger<DellTemperatureMonitor> logger)
    {
        _ipmi = ipmi;
        _lmSensors = lmSensors;
        _logger = logger;
    }

    public async Task<List<TemperatureReading>> GetTemperaturesAsync(CancellationToken cancellationToken = default)
    {
        var readings = new List<TemperatureReading>();
        try
        {
            var sensors = await _ipmi.GetAllSensorsAsync(cancellationToken);
            foreach (var s in sensors)
            {
                if (!IsTemperatureSensor(s)) continue;

                readings.Add(new TemperatureReading
                {
                    Name = s.Name,
                    Value = s.Value,
                    Unit = "°C",
                    Source = TemperatureSource.Ipmi,
                    Timestamp = s.Timestamp,
                    HighThreshold = 75,
                    CriticalThreshold = 90
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read IPMI temperatures");
        }

        return readings;
    }

    public async Task<List<TemperatureReading>> GetCpuCoreTemperaturesAsync(CancellationToken cancellationToken = default)
    {
        if (_coreTempsAvailable == false)
            return new List<TemperatureReading>();

        try
        {
            if (_coreTempsAvailable == null)
                _coreTempsAvailable = await _lmSensors.IsAvailableAsync();

            if (_coreTempsAvailable == false)
                return new List<TemperatureReading>();

            return await _lmSensors.GetCpuCoreTemperaturesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read CPU core temperatures");
            return new List<TemperatureReading>();
        }
    }

    private static bool IsTemperatureSensor(SensorReading s)
    {
        if (string.IsNullOrEmpty(s.Unit)) return false;
        var unit = s.Unit.ToLowerInvariant();
        if (!(unit.Contains("degrees c") || unit.Contains("°c") || unit == "c"))
            return false;

        // Filter out invalid readings
        if (s.Status == "ns" || s.Status == "na") return false;
        if (s.Value <= -50 || s.Value >= 150) return false;

        return true;
    }
}
