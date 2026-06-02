using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Infrastructure.System;

namespace ServerMonitor.Providers.Dell;

/// <summary>
/// Generic Dell PowerEdge provider used as fallback for any Dell server
/// where a more specific provider does not exist
/// </summary>
public class DellGenericProvider : DellProviderBase
{
    public override string Model => "Generic";
    public override string DisplayName => "Dell PowerEdge (Generic)";

    public DellGenericProvider(
        IIpmiClient ipmiClient,
        LmSensorsParser lmSensors,
        ILoggerFactory loggerFactory)
        : base(ipmiClient, lmSensors, loggerFactory)
    {
    }

    // The generic provider matches any Dell server (used as fallback)
    protected override bool SupportsModel(string model) => true;
}
