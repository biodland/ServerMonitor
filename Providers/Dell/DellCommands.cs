namespace ServerMonitor.Providers.Dell;

/// <summary>
/// Dell iDRAC IPMI raw command constants
/// These OEM commands are common across most Dell PowerEdge servers (R610, R620, R720, R720XD, R730, R740 series)
/// </summary>
public static class DellCommands
{
    /// <summary>Enable manual fan control (disable dynamic/automatic mode)</summary>
    public const string EnableManualControl = "0x30 0x30 0x01 0x00";

    /// <summary>Restore automatic fan control (dynamic mode)</summary>
    public const string EnableAutoControl = "0x30 0x30 0x01 0x01";

    /// <summary>
    /// Set fan speed prefix - append a hex byte for the percentage (e.g., 0x14 for 20%)
    /// Full command: 0x30 0x30 0x02 0xff 0x{percentageHex}
    /// </summary>
    public const string SetFanSpeedPrefix = "0x30 0x30 0x02 0xff";

    /// <summary>Get power consumption (Dell-specific)</summary>
    public const string GetPowerConsumption = "0x30 0xa7";

    /// <summary>Power supply sensor name patterns</summary>
    public static readonly string[] PowerSensorNames = new[]
    {
        "Pwr Consumption",
        "Power Consumption",
        "System Level",
        "Total Power"
    };

    /// <summary>Build the IPMI raw command for setting a fan speed percentage</summary>
    public static string BuildSetFanSpeedCommand(int percentage)
    {
        if (percentage < 0) percentage = 0;
        if (percentage > 100) percentage = 100;
        return $"{SetFanSpeedPrefix} 0x{percentage:X2}";
    }
}
