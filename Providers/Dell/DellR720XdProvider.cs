using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Infrastructure.System;

namespace ServerMonitor.Providers.Dell;

/// <summary>
/// Provider for Dell PowerEdge R720 XD
/// </summary>
public class DellR720XdProvider : DellProviderBase
{
    public override string Model => "R720XD";
    public override string DisplayName => "Dell PowerEdge R720 XD";

    public DellR720XdProvider(
        IIpmiClient ipmiClient,
        LmSensorsParser lmSensors,
        ILoggerFactory loggerFactory)
        : base(ipmiClient, lmSensors, loggerFactory)
    {
    }

    protected override bool SupportsModel(string model)
    {
        if (string.IsNullOrEmpty(model)) return false;
        var m = model.ToUpperInvariant().Replace(" ", "");
        return m.Contains("R720XD") || m.Contains("POWEREDGER720XD");
    }
}
