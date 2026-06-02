using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;
using ServerMonitor.Infrastructure.System;

namespace ServerMonitor.Infrastructure.Detection;

/// <summary>
/// Detects server hardware via DMI/SMBIOS information
/// </summary>
public class ServerDetectionService : IServerDetectionService
{
    private readonly ILogger<ServerDetectionService> _logger;
    private readonly ShellExecutor _shell;

    public ServerDetectionService(
        ILogger<ServerDetectionService> logger,
        ShellExecutor shell)
    {
        _logger = logger;
        _shell = shell;
    }

    public async Task<ServerInfo> DetectServerAsync(CancellationToken cancellationToken = default)
    {
        var info = new ServerInfo();

        try
        {
            // Read manufacturer
            info.Manufacturer = await ReadDmiAsync("sys_vendor", cancellationToken);
            
            // Read product name (model)
            info.Model = await ReadDmiAsync("product_name", cancellationToken);
            
            // Read serial number
            info.SerialNumber = await ReadDmiAsync("product_serial", cancellationToken);
            
            // Read BIOS version
            info.BiosVersion = await ReadDmiAsync("bios_version", cancellationToken);
            
            // Try to get hostname
            var hostnameResult = await _shell.ExecuteAsync("hostname", cancellationToken: cancellationToken);
            if (hostnameResult.Success)
                info.Hostname = hostnameResult.StandardOutput.Trim();

            // Identify vendor from manufacturer string
            info.Vendor = IdentifyVendor(info.Manufacturer);
            
            _logger.LogInformation(
                "Detected server: {Manufacturer} {Model} (Vendor: {Vendor})",
                info.Manufacturer, info.Model, info.Vendor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect server hardware");
        }

        return info;
    }

    private async Task<string> ReadDmiAsync(string property, CancellationToken cancellationToken)
    {
        var paths = new[]
        {
            $"/sys/class/dmi/id/{property}",
            $"/sys/devices/virtual/dmi/id/{property}"
        };

        foreach (var path in paths)
        {
            var result = await _shell.ExecuteAsync($"cat {path} 2>/dev/null", cancellationToken: cancellationToken);
            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return result.StandardOutput.Trim();
            }
        }

        // Fallback to dmidecode
        var dmiResult = await _shell.ExecuteAsync(
            $"dmidecode -s {property.Replace("_", "-")} 2>/dev/null", 
            cancellationToken: cancellationToken);
        if (dmiResult.Success)
            return dmiResult.StandardOutput.Trim();

        return string.Empty;
    }

    /// <summary>
    /// Identify vendor from manufacturer string
    /// </summary>
    public static ServerVendor IdentifyVendor(string manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer))
            return ServerVendor.Unknown;

        var lower = manufacturer.ToLowerInvariant();

        if (lower.Contains("dell"))
            return ServerVendor.Dell;
        if (lower.Contains("supermicro") || lower.Contains("super micro"))
            return ServerVendor.SuperMicro;
        if (lower.Contains("asrock"))
            return ServerVendor.AsRock;
        if (lower.Contains("hewlett") || lower.Contains("hpe") || lower.Contains("hp"))
            return ServerVendor.Hpe;
        if (lower.Contains("lenovo"))
            return ServerVendor.Lenovo;
        if (lower.Contains("ibm"))
            return ServerVendor.IBM;

        return ServerVendor.Generic;
    }
}
