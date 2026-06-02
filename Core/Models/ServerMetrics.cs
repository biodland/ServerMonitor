using ServerMonitor.Core.Enums;

namespace ServerMonitor.Core.Models;

/// <summary>
/// Aggregated server metrics - a complete snapshot of server state
/// </summary>
public class ServerMetrics
{
    public ServerInfo Server { get; set; } = new();
    public List<TemperatureReading> Temperatures { get; set; } = new();
    public List<TemperatureReading> CpuCores { get; set; } = new();
    public List<FanReading> Fans { get; set; } = new();
    public PowerReading? Power { get; set; }
    public List<GpuReading> Gpus { get; set; } = new();
    public FanControlMode CurrentMode { get; set; } = FanControlMode.Unknown;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Maximum temperature across all sensors
    /// </summary>
    public double MaxTemperature => 
        Temperatures.Concat(CpuCores).DefaultIfEmpty().Max(t => t?.Value ?? 0);
    
    /// <summary>
    /// Average fan RPM
    /// </summary>
    public int AverageRpm =>
        Fans.Count > 0 ? (int)Fans.Average(f => f.RPM) : 0;
    
    /// <summary>
    /// Overall system health based on all sensors
    /// </summary>
    public HealthStatus OverallHealth
    {
        get
        {
            var allTemps = Temperatures.Concat(CpuCores);
            if (allTemps.Any(t => t.Status == HealthStatus.Critical))
                return HealthStatus.Critical;
            if (allTemps.Any(t => t.Status == HealthStatus.Warning))
                return HealthStatus.Warning;
            return HealthStatus.Good;
        }
    }
}
