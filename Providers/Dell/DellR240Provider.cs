using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Infrastructure.System;

namespace ServerMonitor.Providers.Dell;

/// <summary>
/// Provider for Dell PowerEdge R240
/// </summary>
public class DellR240Provider : DellProviderBase
{
    public override string Model => "R240";
    public override string DisplayName => "Dell PowerEdge R240";

    public DellR240Provider(
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
        return m.Contains("R240") || m.Contains("POWEREDGER240");
    }
}
