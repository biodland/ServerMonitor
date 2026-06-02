using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Core.Interfaces;

/// <summary>
/// Main server provider interface
/// Each server vendor/model implements this interface
/// </summary>
public interface IServerProvider
{
    /// <summary>
    /// The server vendor (Dell, SuperMicro, etc.)
    /// </summary>
    ServerVendor Vendor { get; }
    
    /// <summary>
    /// The server model identifier (e.g., "R720XD", "R740XD")
    /// </summary>
    string Model { get; }
    
    /// <summary>
    /// Display name for the provider
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Check if this provider can handle the detected server
    /// </summary>
    bool IsSupported(ServerInfo serverInfo);
    
    /// <summary>
    /// Initialize the provider (called once after creation)
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get server hardware information
    /// </summary>
    Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Test if the provider can communicate with the hardware
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Temperature monitoring component
    /// </summary>
    ITemperatureMonitor TemperatureMonitor { get; }
    
    /// <summary>
    /// Fan control component
    /// </summary>
    IFanController FanController { get; }
    
    /// <summary>
    /// Power monitoring component (may return no-op implementation if unsupported)
    /// </summary>
    IPowerMonitor PowerMonitor { get; }
}

/// <summary>
/// Factory for creating appropriate server provider
/// </summary>
public interface IServerProviderFactory
{
    /// <summary>
    /// Create the best matching server provider based on detected hardware
    /// </summary>
    Task<IServerProvider> CreateProviderAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all registered providers
    /// </summary>
    IEnumerable<IServerProvider> GetAvailableProviders();
}

/// <summary>
/// Server hardware detection service
/// </summary>
public interface IServerDetectionService
{
    /// <summary>
    /// Detect the server hardware via DMI/SMBIOS or other means
    /// </summary>
    Task<ServerInfo> DetectServerAsync(CancellationToken cancellationToken = default);
}
