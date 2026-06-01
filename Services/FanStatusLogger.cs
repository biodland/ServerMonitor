using Microsoft.Extensions.Logging;

namespace DellFanControl.Services;

/// <summary>
/// Logs fan and temperature status, both to file logger and maintains current state
/// </summary>
public class FanStatusLogger
{
    private readonly ILogger<FanStatusLogger> _logger;
    private readonly object _lock = new();

    public FanStatusLogger(ILogger<FanStatusLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Current system state (thread-safe access)
    /// </summary>
    public SystemStatus CurrentStatus { get; private set; } = new();

    public void LogStatus(TemperatureStatus tempStatus, FanStatus fanStatus, string mode)
    {
        lock (_lock)
        {
            // Update current state
            CurrentStatus = new SystemStatus
            {
                TemperatureStatus = tempStatus,
                FanStatus = fanStatus,
                FanControlMode = mode,
                LastUpdateTime = DateTime.Now
            };
        }

        // Log to console
        _logger.LogInformation(
            "Status Update | Mode: {Mode} | Max Temp: {MaxTemp}°C | Avg Fan RPM: {AvgRPM} | Total Fans: {FanCount}",
            mode,
            tempStatus.HighestTempCelsius,
            fanStatus.AverageRPM,
            fanStatus.Fans.Count
        );
    }

    /// <summary>
    /// Get current system status (thread-safe)
    /// </summary>
    public SystemStatus GetCurrentStatus()
    {
        lock (_lock)
        {
            // Return a copy to avoid concurrent modifications
            return new SystemStatus
            {
                TemperatureStatus = new TemperatureStatus
                {
                    HighestTempCelsius = CurrentStatus.TemperatureStatus.HighestTempCelsius,
                    AllTemperatures = new List<int>(CurrentStatus.TemperatureStatus.AllTemperatures),
                    Timestamp = CurrentStatus.TemperatureStatus.Timestamp,
                    LastUpdateTime = CurrentStatus.TemperatureStatus.LastUpdateTime
                },
                FanStatus = new FanStatus
                {
                    Fans = CurrentStatus.FanStatus.Fans.Select(f => new FanInfo
                    {
                        Name = f.Name,
                        RPM = f.RPM,
                        Timestamp = f.Timestamp
                    }).ToList(),
                    Timestamp = CurrentStatus.FanStatus.Timestamp,
                    LastUpdateTime = CurrentStatus.FanStatus.LastUpdateTime
                },
                FanControlMode = CurrentStatus.FanControlMode,
                LastUpdateTime = CurrentStatus.LastUpdateTime
            };
        }
    }
}

/// <summary>
/// Complete system status information
/// </summary>
public class SystemStatus
{
    public TemperatureStatus TemperatureStatus { get; set; } = new();
    public FanStatus FanStatus { get; set; } = new();
    public string FanControlMode { get; set; } = "Unknown";
    public DateTime LastUpdateTime { get; set; } = DateTime.Now;
}