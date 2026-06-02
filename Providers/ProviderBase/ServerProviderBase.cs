using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Providers.ProviderBase;

/// <summary>
/// Base class for all server providers
/// Provides common functionality and template methods
/// </summary>
public abstract class ServerProviderBase : IServerProvider
{
    protected readonly IIpmiClient IpmiClient;
    protected readonly ILogger Logger;
    private bool _initialized;

    public abstract ServerVendor Vendor { get; }
    public abstract string Model { get; }
    public virtual string DisplayName => $"{Vendor} {Model}";
    
    public abstract ITemperatureMonitor TemperatureMonitor { get; }
    public abstract IFanController FanController { get; }
    public abstract IPowerMonitor PowerMonitor { get; }

    protected ServerProviderBase(IIpmiClient ipmiClient, ILogger logger)
    {
        IpmiClient = ipmiClient;
        Logger = logger;
    }

    public abstract bool IsSupported(ServerInfo serverInfo);

    public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        
        Logger.LogInformation("Initializing {Provider} provider...", DisplayName);
        await IpmiClient.TestConnectionAsync(cancellationToken);
        _initialized = true;
    }

    public virtual async Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = new ServerInfo
        {
            Vendor = Vendor,
            Model = Model
        };

        try
        {
            var bmcInfo = await IpmiClient.GetBmcInfoAsync(cancellationToken);
            
            if (bmcInfo.TryGetValue("Manufacturer Name", out var manuf))
                info.Manufacturer = manuf;
            
            if (bmcInfo.TryGetValue("IPMI Version", out var version))
                info.IpmiVersion = version;

            foreach (var kvp in bmcInfo)
                info.Properties[kvp.Key] = kvp.Value;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not retrieve full server info");
        }

        return info;
    }

    public virtual Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return IpmiClient.TestConnectionAsync(cancellationToken);
    }
}
