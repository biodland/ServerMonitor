namespace ServerMonitor.Core.Models;

/// <summary>
/// CPU load and utilization metrics
/// </summary>
public class CpuStats
{
    /// <summary>Overall CPU usage percentage (0-100)</summary>
    public double UsagePercent { get; set; }

    /// <summary>Per-core CPU usage percentages</summary>
    public List<double> PerCoreUsage { get; set; } = new();

    /// <summary>Number of logical cores</summary>
    public int CoreCount { get; set; }

    /// <summary>Load average over 1 minute</summary>
    public double LoadAverage1Min { get; set; }

    /// <summary>Load average over 5 minutes</summary>
    public double LoadAverage5Min { get; set; }

    /// <summary>Load average over 15 minutes</summary>
    public double LoadAverage15Min { get; set; }

    /// <summary>User-mode CPU percentage</summary>
    public double UserPercent { get; set; }

    /// <summary>System (kernel) CPU percentage</summary>
    public double SystemPercent { get; set; }

    /// <summary>Idle CPU percentage</summary>
    public double IdlePercent { get; set; }

    /// <summary>I/O wait CPU percentage</summary>
    public double IoWaitPercent { get; set; }

    /// <summary>CPU model name (e.g. "Intel Xeon E5-2640 v2")</summary>
    public string ModelName { get; set; } = string.Empty;
}

/// <summary>
/// Memory usage and utilization metrics
/// </summary>
public class MemoryStats
{
    /// <summary>Total physical memory in bytes</summary>
    public long TotalBytes { get; set; }

    /// <summary>Used memory in bytes (excluding cache/buffers)</summary>
    public long UsedBytes { get; set; }

    /// <summary>Free memory in bytes</summary>
    public long FreeBytes { get; set; }

    /// <summary>Available memory in bytes (the kernel's MemAvailable estimate)</summary>
    public long AvailableBytes { get; set; }

    /// <summary>Memory used by buffers</summary>
    public long BuffersBytes { get; set; }

    /// <summary>Memory used by page cache</summary>
    public long CachedBytes { get; set; }

    /// <summary>Total swap space in bytes</summary>
    public long SwapTotalBytes { get; set; }

    /// <summary>Used swap space in bytes</summary>
    public long SwapUsedBytes { get; set; }

    /// <summary>Memory usage percentage (0-100)</summary>
    public double UsagePercent =>
        TotalBytes > 0 ? (UsedBytes * 100.0) / TotalBytes : 0;

    /// <summary>Swap usage percentage (0-100)</summary>
    public double SwapUsagePercent =>
        SwapTotalBytes > 0 ? (SwapUsedBytes * 100.0) / SwapTotalBytes : 0;
}

/// <summary>
/// Statistics for a single network interface
/// </summary>
public class NetworkInterfaceStats
{
    /// <summary>Interface name (e.g. "eth0", "ens1f0")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the interface is currently up</summary>
    public bool IsUp { get; set; }

    /// <summary>Link speed in Mbps (0 if unknown / down)</summary>
    public long SpeedMbps { get; set; }

    /// <summary>MAC address</summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>IPv4 address(es) assigned to the interface</summary>
    public List<string> IpAddresses { get; set; } = new();

    /// <summary>Total bytes received since boot</summary>
    public long BytesReceived { get; set; }

    /// <summary>Total bytes transmitted since boot</summary>
    public long BytesSent { get; set; }

    /// <summary>Total packets received since boot</summary>
    public long PacketsReceived { get; set; }

    /// <summary>Total packets transmitted since boot</summary>
    public long PacketsSent { get; set; }

    /// <summary>Receive errors since boot</summary>
    public long ReceiveErrors { get; set; }

    /// <summary>Transmit errors since boot</summary>
    public long TransmitErrors { get; set; }

    /// <summary>Bytes/second received (delta-based, computed by collector)</summary>
    public double ReceiveBytesPerSec { get; set; }

    /// <summary>Bytes/second transmitted (delta-based, computed by collector)</summary>
    public double TransmitBytesPerSec { get; set; }
}

/// <summary>
/// Statistics for a mounted filesystem (storage volume)
/// </summary>
public class StorageVolumeStats
{
    /// <summary>Mount point (e.g. "/", "/mnt/data")</summary>
    public string MountPoint { get; set; } = string.Empty;

    /// <summary>Underlying device (e.g. "/dev/sda1")</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Filesystem type (e.g. "ext4", "xfs", "zfs")</summary>
    public string FilesystemType { get; set; } = string.Empty;

    /// <summary>Total capacity in bytes</summary>
    public long TotalBytes { get; set; }

    /// <summary>Used space in bytes</summary>
    public long UsedBytes { get; set; }

    /// <summary>Available space in bytes</summary>
    public long AvailableBytes { get; set; }

    /// <summary>Usage percentage (0-100)</summary>
    public double UsagePercent =>
        TotalBytes > 0 ? (UsedBytes * 100.0) / TotalBytes : 0;
}

/// <summary>
/// Statistics for a physical block device (disk)
/// </summary>
public class StorageDeviceStats
{
    /// <summary>Device name (e.g. "sda", "nvme0n1")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Model string (e.g. "Samsung SSD 970 EVO 500GB")</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Device size in bytes</summary>
    public long SizeBytes { get; set; }

    /// <summary>Whether the device is rotational (HDD) or solid-state</summary>
    public bool IsRotational { get; set; }

    /// <summary>Read bytes/second (delta-based)</summary>
    public double ReadBytesPerSec { get; set; }

    /// <summary>Write bytes/second (delta-based)</summary>
    public double WriteBytesPerSec { get; set; }

    /// <summary>Reads/second (delta-based)</summary>
    public double ReadsPerSec { get; set; }

    /// <summary>Writes/second (delta-based)</summary>
    public double WritesPerSec { get; set; }

    /// <summary>Drive temperature (°C) if available, else null</summary>
    public double? TemperatureCelsius { get; set; }

    /// <summary>Total bytes read since boot</summary>
    public long BytesRead { get; set; }

    /// <summary>Total bytes written since boot</summary>
    public long BytesWritten { get; set; }
}

/// <summary>
/// Aggregated system statistics - a complete snapshot of system state
/// (CPU / Memory / Network / Storage) - distinct from server hardware
/// metrics which live in <see cref="ServerMetrics"/>.
/// </summary>
public class SystemStats
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public CpuStats Cpu { get; set; } = new();

    public MemoryStats Memory { get; set; } = new();

    public List<NetworkInterfaceStats> NetworkInterfaces { get; set; } = new();

    public List<StorageVolumeStats> StorageVolumes { get; set; } = new();

    public List<StorageDeviceStats> StorageDevices { get; set; } = new();

    /// <summary>System uptime in seconds</summary>
    public long UptimeSeconds { get; set; }

    /// <summary>Hostname</summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>Kernel version</summary>
    public string KernelVersion { get; set; } = string.Empty;
}
