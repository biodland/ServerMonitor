using ServerMonitor.Core.Enums;

namespace ServerMonitor.Core.Models;

/// <summary>
/// Generic sensor reading
/// </summary>
public class SensorReading
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = "ok";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Temperature reading with thresholds
/// </summary>
public class TemperatureReading
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = "°C";
    public double? HighThreshold { get; set; }
    public double? CriticalThreshold { get; set; }
    public TemperatureSource Source { get; set; } = TemperatureSource.Unknown;
    public bool IsPackage { get; set; } = false;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public HealthStatus Status
    {
        get
        {
            if (CriticalThreshold.HasValue && Value >= CriticalThreshold.Value)
                return HealthStatus.Critical;
            if (HighThreshold.HasValue && Value >= HighThreshold.Value)
                return HealthStatus.Warning;
            return HealthStatus.Good;
        }
    }
}

/// <summary>
/// Fan reading with control info
/// </summary>
public class FanReading
{
    public string Name { get; set; } = string.Empty;
    public int RPM { get; set; }
    public int? PercentageSpeed { get; set; }
    public bool IsHealthy { get; set; } = true;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Power consumption reading
/// </summary>
public class PowerReading
{
    public double TotalWatts { get; set; }
    public double? CpuWatts { get; set; }
    public double? PowerSupplyWatts { get; set; }
    public string Source { get; set; } = "Unknown";
    public bool IsAvailable { get; set; } = true;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// GPU reading
/// </summary>
public class GpuReading
{
    public string Name { get; set; } = string.Empty;
    public GpuVendor Vendor { get; set; } = GpuVendor.Unknown;
    public double Temperature { get; set; }
    public int? FanSpeedPercent { get; set; }
    public double? PowerWatts { get; set; }
    public int? UsagePercent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
