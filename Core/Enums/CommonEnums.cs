namespace ServerMonitor.Core.Enums;

/// <summary>
/// Source of temperature reading
/// </summary>
public enum TemperatureSource
{
    Unknown,
    Ipmi,
    LmSensors,
    Gpu,
    Disk,
    Estimated
}

/// <summary>
/// IPMI client type
/// </summary>
public enum IpmiClientType
{
    /// <summary>In-band IPMI via local ipmitool</summary>
    InBand,
    /// <summary>Out-of-band IPMI via network ipmitool</summary>
    OutBand,
    /// <summary>Native IPMI implementation (future)</summary>
    Native
}

/// <summary>
/// GPU vendor
/// </summary>
public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}
