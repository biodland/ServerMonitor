using ServerMonitor.Core.Models;

namespace ServerMonitor.Core.Interfaces;

/// <summary>
/// Temperature monitoring abstraction
/// </summary>
public interface ITemperatureMonitor
{
    /// <summary>
    /// Get all temperature readings from IPMI sensors
    /// </summary>
    Task<List<TemperatureReading>> GetTemperaturesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get CPU core temperatures from lm-sensors or similar
    /// </summary>
    Task<List<TemperatureReading>> GetCpuCoreTemperaturesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Whether CPU core temperature monitoring is available
    /// </summary>
    bool SupportsCoreTemperatures { get; }
}

/// <summary>
/// Fan control and monitoring abstraction
/// </summary>
public interface IFanController
{
    /// <summary>
    /// Get current fan readings
    /// </summary>
    Task<List<FanReading>> GetFanStatusAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set manual fan speed (0-100%)
    /// </summary>
    Task<bool> SetFanSpeedAsync(int percentage, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Restore automatic/dynamic fan control
    /// </summary>
    Task<bool> RestoreAutoControlAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Whether manual fan control is supported
    /// </summary>
    bool SupportsManualControl { get; }
    
    /// <summary>
    /// Minimum allowed fan speed percentage
    /// </summary>
    int MinSpeedPercentage { get; }
    
    /// <summary>
    /// Maximum allowed fan speed percentage
    /// </summary>
    int MaxSpeedPercentage { get; }
}

/// <summary>
/// Power consumption monitoring abstraction
/// </summary>
public interface IPowerMonitor
{
    /// <summary>
    /// Get current power consumption metrics
    /// </summary>
    Task<PowerReading> GetPowerMetricsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Whether power monitoring is supported by this hardware
    /// </summary>
    bool IsSupported { get; }
}

/// <summary>
/// GPU monitoring abstraction
/// </summary>
public interface IGpuMonitor
{
    /// <summary>
    /// Get all GPU readings (NVIDIA, AMD, Intel)
    /// </summary>
    Task<List<GpuReading>> GetGpuReadingsAsync(CancellationToken cancellationToken = default);
}
