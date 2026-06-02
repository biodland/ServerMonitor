using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Infrastructure.System;

namespace ServerMonitor.Providers.Dell;

/// <summary>
/// Provider for Dell PowerEdge R740 XD
/// Shares same IPMI OEM commands as the R720 XD generation
/// </summary>
public class DellR740XdProvider : DellProviderBase
{
    public override string Model => "R740XD";
    public override string DisplayName => "Dell PowerEdge R740 XD";

    public DellR740XdProvider(
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
        return m.Contains("R740XD") || m.Contains("POWEREDGER740XD");
    }
}
