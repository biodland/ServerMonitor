using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;
using ServerMonitor.Infrastructure.System;
using ServerMonitor.Providers.ProviderBase;

namespace ServerMonitor.Providers.Dell;

/// <summary>
/// Base class for all Dell PowerEdge providers
/// Most Dell PowerEdge servers share the same IPMI OEM commands for fan control
/// </summary>
public abstract class DellProviderBase : ServerProviderBase
{
    public override ServerVendor Vendor => ServerVendor.Dell;

    public override ITemperatureMonitor TemperatureMonitor { get; }
    public override IFanController FanController { get; }
    public override IPowerMonitor PowerMonitor { get; }

    protected DellProviderBase(
        IIpmiClient ipmiClient,
        LmSensorsParser lmSensors,
        ILoggerFactory loggerFactory)
        : base(ipmiClient, loggerFactory.CreateLogger<DellProviderBase>())
    {
        TemperatureMonitor = new DellTemperatureMonitor(
            ipmiClient,
            lmSensors,
            loggerFactory.CreateLogger<DellTemperatureMonitor>());

        FanController = new DellFanController(
            ipmiClient,
            loggerFactory.CreateLogger<DellFanController>());

        PowerMonitor = new DellPowerMonitor(
            ipmiClient,
            loggerFactory.CreateLogger<DellPowerMonitor>());
    }

    public override bool IsSupported(ServerInfo serverInfo)
    {
        if (serverInfo.Vendor != ServerVendor.Dell) return false;
        return SupportsModel(serverInfo.Model);
    }

    /// <summary>
    /// Check if a given model string matches this provider's supported model(s)
    /// </summary>
    protected abstract bool SupportsModel(string model);
}
