using ServerMonitor.Core.Enums;

namespace ServerMonitor.Core.Models;

/// <summary>
/// Server hardware information
/// </summary>
public class ServerInfo
{
    public ServerVendor Vendor { get; set; } = ServerVendor.Unknown;
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;
    public string IpmiVersion { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
    
    public string DisplayName => $"{Manufacturer} {Model}".Trim();
}
