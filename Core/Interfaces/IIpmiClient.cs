using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Core.Interfaces;

/// <summary>
/// Abstraction over IPMI communication
/// Different implementations: ipmitool, native IPMI, etc.
/// </summary>
public interface IIpmiClient
{
    /// <summary>
    /// The type of IPMI client (InBand, OutBand, Native)
    /// </summary>
    IpmiClientType ClientType { get; }
    
    /// <summary>
    /// Whether the client is currently connected/available
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// Test the IPMI connection
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute a raw IPMI command
    /// </summary>
    Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute raw IPMI bytes (for OEM commands)
    /// </summary>
    Task<string> ExecuteRawAsync(string rawHex, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all sensor readings via IPMI sensor command
    /// </summary>
    Task<List<SensorReading>> GetAllSensorsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get BMC/iDRAC information
    /// </summary>
    Task<Dictionary<string, string>> GetBmcInfoAsync(CancellationToken cancellationToken = default);
}
