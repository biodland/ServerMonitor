using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Infrastructure.System;

/// <summary>
/// Collects system stats on Linux by reading /proc and /sys directly.
/// Uses external tools (df, smartctl) only where /proc/sys does not
/// expose the data in a convenient form.
/// </summary>
public class LinuxSystemStatsCollector : ISystemStatsCollector
{
    private readonly ILogger<LinuxSystemStatsCollector> _logger;
    private readonly ShellExecutor _shell;

    // Previous samples for delta calculations
    private CpuSample? _prevCpuSample;
    private readonly Dictionary<int, CpuSample> _prevPerCpuSamples = new();
    private readonly Dictionary<string, NetSample> _prevNetSamples = new();
    private readonly Dictionary<string, DiskSample> _prevDiskSamples = new();
    private DateTime _prevSampleTime = DateTime.MinValue;

    public LinuxSystemStatsCollector(
        ILogger<LinuxSystemStatsCollector> logger,
        ShellExecutor shell)
    {
        _logger = logger;
        _shell = shell;
    }

    public async Task<SystemStats> CollectAsync(CancellationToken cancellationToken = default)
    {
        var stats = new SystemStats { Timestamp = DateTime.UtcNow };
        var now = stats.Timestamp;
        var elapsedSeconds = _prevSampleTime == DateTime.MinValue
            ? 0
            : (now - _prevSampleTime).TotalSeconds;

        // Run independent collectors concurrently
        var cpuTask = CollectCpuAsync(elapsedSeconds, cancellationToken);
        var memTask = CollectMemoryAsync(cancellationToken);
        var netTask = CollectNetworkAsync(elapsedSeconds, cancellationToken);
        var volTask = CollectVolumesAsync(cancellationToken);
        var diskTask = CollectDevicesAsync(elapsedSeconds, cancellationToken);
        var infoTask = CollectGeneralInfoAsync(cancellationToken);

        await Task.WhenAll(cpuTask, memTask, netTask, volTask, diskTask, infoTask);

        stats.Cpu = cpuTask.Result;
        stats.Memory = memTask.Result;
        stats.NetworkInterfaces = netTask.Result;
        stats.StorageVolumes = volTask.Result;
        stats.StorageDevices = diskTask.Result;

        var (uptime, hostname, kernel) = infoTask.Result;
        stats.UptimeSeconds = uptime;
        stats.Hostname = hostname;
        stats.KernelVersion = kernel;

        _prevSampleTime = now;
        return stats;
    }

    // ------------------------------ CPU ------------------------------

    private async Task<CpuStats> CollectCpuAsync(double elapsedSeconds, CancellationToken ct)
    {
        var cpu = new CpuStats();
        try
        {
            var content = await ReadFileAsync("/proc/stat", ct);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("cpu ", StringComparison.Ordinal))
                {
                    var sample = ParseCpuLine(line);
                    if (sample != null && _prevCpuSample != null)
                    {
                        var (usage, user, system, idle, iowait) = ComputeCpuPercents(_prevCpuSample, sample);
                        cpu.UsagePercent = usage;
                        cpu.UserPercent = user;
                        cpu.SystemPercent = system;
                        cpu.IdlePercent = idle;
                        cpu.IoWaitPercent = iowait;
                    }
                    if (sample != null) _prevCpuSample = sample;
                }
                else if (line.StartsWith("cpu", StringComparison.Ordinal) && char.IsDigit(line[3]))
                {
                    // Per-core: "cpu0 ..."
                    var spaceIdx = line.IndexOf(' ');
                    if (spaceIdx <= 3) continue;
                    if (!int.TryParse(line.AsSpan(3, spaceIdx - 3), out var coreIdx)) continue;

                    var sample = ParseCpuLine(line);
                    if (sample == null) continue;

                    if (_prevPerCpuSamples.TryGetValue(coreIdx, out var prev))
                    {
                        var (usage, _, _, _, _) = ComputeCpuPercents(prev, sample);
                        // Pad list to required length
                        while (cpu.PerCoreUsage.Count <= coreIdx) cpu.PerCoreUsage.Add(0);
                        cpu.PerCoreUsage[coreIdx] = usage;
                    }
                    _prevPerCpuSamples[coreIdx] = sample;
                }
            }

            cpu.CoreCount = _prevPerCpuSamples.Count;
            if (cpu.CoreCount > 0 && cpu.PerCoreUsage.Count < cpu.CoreCount)
            {
                while (cpu.PerCoreUsage.Count < cpu.CoreCount) cpu.PerCoreUsage.Add(0);
            }

            // Load averages
            try
            {
                var loadavg = await ReadFileAsync("/proc/loadavg", ct);
                var parts = loadavg.Trim().Split(' ');
                if (parts.Length >= 3)
                {
                    cpu.LoadAverage1Min = ParseDouble(parts[0]);
                    cpu.LoadAverage5Min = ParseDouble(parts[1]);
                    cpu.LoadAverage15Min = ParseDouble(parts[2]);
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "loadavg read failed"); }

            // CPU model name
            try
            {
                var cpuinfo = await ReadFileAsync("/proc/cpuinfo", ct);
                var modelMatch = Regex.Match(cpuinfo, @"model name\s*:\s*(.+)");
                if (modelMatch.Success)
                    cpu.ModelName = modelMatch.Groups[1].Value.Trim();
            }
            catch (Exception ex) { _logger.LogDebug(ex, "cpuinfo read failed"); }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect CPU stats");
        }

        return cpu;
    }

    private static CpuSample? ParseCpuLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // cpu user nice system idle iowait irq softirq steal guest guest_nice
        if (parts.Length < 5) return null;

        try
        {
            var s = new CpuSample
            {
                User = long.Parse(parts[1]),
                Nice = long.Parse(parts[2]),
                System = long.Parse(parts[3]),
                Idle = long.Parse(parts[4]),
                IoWait = parts.Length > 5 ? long.Parse(parts[5]) : 0,
                Irq = parts.Length > 6 ? long.Parse(parts[6]) : 0,
                SoftIrq = parts.Length > 7 ? long.Parse(parts[7]) : 0,
                Steal = parts.Length > 8 ? long.Parse(parts[8]) : 0,
            };
            return s;
        }
        catch
        {
            return null;
        }
    }

    private static (double usage, double user, double system, double idle, double iowait)
        ComputeCpuPercents(CpuSample prev, CpuSample curr)
    {
        var prevTotal = prev.Total;
        var currTotal = curr.Total;
        var totalDelta = currTotal - prevTotal;
        if (totalDelta <= 0) return (0, 0, 0, 0, 0);

        var idleDelta = (curr.Idle + curr.IoWait) - (prev.Idle + prev.IoWait);
        var usage = 100.0 * (totalDelta - idleDelta) / totalDelta;
        var user = 100.0 * ((curr.User + curr.Nice) - (prev.User + prev.Nice)) / totalDelta;
        var system = 100.0 * (curr.System - prev.System) / totalDelta;
        var idle = 100.0 * (curr.Idle - prev.Idle) / totalDelta;
        var iowait = 100.0 * (curr.IoWait - prev.IoWait) / totalDelta;

        return (Clamp(usage), Clamp(user), Clamp(system), Clamp(idle), Clamp(iowait));
    }

    // ------------------------------ Memory ------------------------------

    private async Task<MemoryStats> CollectMemoryAsync(CancellationToken ct)
    {
        var mem = new MemoryStats();
        try
        {
            var content = await ReadFileAsync("/proc/meminfo", ct);
            var values = ParseKeyValueKb(content);

            mem.TotalBytes = GetKbAsBytes(values, "MemTotal");
            mem.FreeBytes = GetKbAsBytes(values, "MemFree");
            mem.AvailableBytes = GetKbAsBytes(values, "MemAvailable");
            mem.BuffersBytes = GetKbAsBytes(values, "Buffers");
            mem.CachedBytes = GetKbAsBytes(values, "Cached") + GetKbAsBytes(values, "SReclaimable");
            mem.SwapTotalBytes = GetKbAsBytes(values, "SwapTotal");
            var swapFree = GetKbAsBytes(values, "SwapFree");
            mem.SwapUsedBytes = mem.SwapTotalBytes - swapFree;

            // "Used" = Total - Available (the modern Linux convention used by `free -m`)
            mem.UsedBytes = mem.AvailableBytes > 0
                ? mem.TotalBytes - mem.AvailableBytes
                : mem.TotalBytes - mem.FreeBytes - mem.BuffersBytes - mem.CachedBytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect memory stats");
        }
        return mem;
    }

    // ------------------------------ Network ------------------------------

    private async Task<List<NetworkInterfaceStats>> CollectNetworkAsync(double elapsed, CancellationToken ct)
    {
        var results = new List<NetworkInterfaceStats>();
        try
        {
            var content = await ReadFileAsync("/proc/net/dev", ct);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Skip the first 2 header lines
            foreach (var rawLine in lines.Skip(2))
            {
                var colon = rawLine.IndexOf(':');
                if (colon < 0) continue;

                var name = rawLine.Substring(0, colon).Trim();
                if (string.IsNullOrEmpty(name) || ShouldSkipInterface(name)) continue;

                var fields = rawLine.Substring(colon + 1)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 16) continue;

                var iface = new NetworkInterfaceStats
                {
                    Name = name,
                    BytesReceived = ParseLong(fields[0]),
                    PacketsReceived = ParseLong(fields[1]),
                    ReceiveErrors = ParseLong(fields[2]),
                    BytesSent = ParseLong(fields[8]),
                    PacketsSent = ParseLong(fields[9]),
                    TransmitErrors = ParseLong(fields[10]),
                };

                // Compute rates from previous sample
                if (elapsed > 0 && _prevNetSamples.TryGetValue(name, out var prev))
                {
                    iface.ReceiveBytesPerSec = Math.Max(0,
                        (iface.BytesReceived - prev.BytesReceived) / elapsed);
                    iface.TransmitBytesPerSec = Math.Max(0,
                        (iface.BytesSent - prev.BytesSent) / elapsed);
                }

                _prevNetSamples[name] = new NetSample
                {
                    BytesReceived = iface.BytesReceived,
                    BytesSent = iface.BytesSent
                };

                // Enrich with sysfs metadata
                await EnrichNetworkInterfaceAsync(iface, ct);

                results.Add(iface);
            }

            // Sort: prefer interfaces that are up + with traffic, then by name
            results = results
                .OrderByDescending(i => i.IsUp)
                .ThenByDescending(i => i.BytesReceived + i.BytesSent)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect network stats");
        }
        return results;
    }

    private static bool ShouldSkipInterface(string name)
    {
        // Skip loopback and well-known virtual interface prefixes by default.
        // (Configuration filtering happens at the service layer.)
        if (name == "lo") return false; // include lo - users may want to see it
        return false;
    }

    private async Task EnrichNetworkInterfaceAsync(NetworkInterfaceStats iface, CancellationToken ct)
    {
        try
        {
            var basePath = $"/sys/class/net/{iface.Name}";

            var operstate = await TryReadFileAsync($"{basePath}/operstate", ct);
            iface.IsUp = string.Equals(operstate?.Trim(), "up", StringComparison.OrdinalIgnoreCase);

            var speed = await TryReadFileAsync($"{basePath}/speed", ct);
            if (!string.IsNullOrEmpty(speed) && long.TryParse(speed.Trim(), out var s) && s > 0)
                iface.SpeedMbps = s;

            var mac = await TryReadFileAsync($"{basePath}/address", ct);
            if (!string.IsNullOrEmpty(mac))
                iface.MacAddress = mac.Trim();

            // IP addresses via `ip -4 -o addr show <iface>`
            var ipResult = await _shell.ExecuteAsync(
                $"ip -4 -o addr show {iface.Name} 2>/dev/null | awk '{{print $4}}' | cut -d/ -f1",
                timeoutMs: 3000, cancellationToken: ct);
            if (ipResult.Success)
            {
                iface.IpAddresses = ipResult.StandardOutput
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enrich network interface {Name}", iface.Name);
        }
    }

    // ------------------------------ Storage volumes (filesystems) ------------------------------

    private async Task<List<StorageVolumeStats>> CollectVolumesAsync(CancellationToken ct)
    {
        var results = new List<StorageVolumeStats>();
        try
        {
            // Use df with byte counts and POSIX output.
            // Exclude virtual / container-only filesystems by default.
            var result = await _shell.ExecuteAsync(
                "df -B1 -PT -x tmpfs -x devtmpfs -x squashfs -x overlay -x proc -x sysfs -x cgroup -x cgroup2 -x tracefs -x debugfs -x configfs -x securityfs -x pstore -x bpf -x ramfs -x autofs -x mqueue 2>/dev/null",
                timeoutMs: 5000, cancellationToken: ct);

            if (!result.Success) return results;

            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Skip(1))
            {
                // Filesystem  Type  1B-blocks  Used  Available  Capacity  Mounted on
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 7) continue;

                var vol = new StorageVolumeStats
                {
                    Device = parts[0],
                    FilesystemType = parts[1],
                    TotalBytes = ParseLong(parts[2]),
                    UsedBytes = ParseLong(parts[3]),
                    AvailableBytes = ParseLong(parts[4]),
                    MountPoint = string.Join(' ', parts.Skip(6))
                };
                if (vol.TotalBytes > 0)
                    results.Add(vol);
            }

            results = results.OrderByDescending(v => v.TotalBytes).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect storage volume stats");
        }
        return results;
    }

    // ------------------------------ Storage devices (block devices) ------------------------------

    private async Task<List<StorageDeviceStats>> CollectDevicesAsync(double elapsed, CancellationToken ct)
    {
        var results = new List<StorageDeviceStats>();
        try
        {
            // List physical block devices via /sys/block (skip loop, ram, dm/sr by default)
            var listResult = await _shell.ExecuteAsync(
                "ls /sys/block 2>/dev/null", timeoutMs: 3000, cancellationToken: ct);
            if (!listResult.Success) return results;

            var devices = listResult.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(d => !d.StartsWith("loop") && !d.StartsWith("ram") && !d.StartsWith("sr") && !d.StartsWith("dm-"))
                .ToList();

            // Read /proc/diskstats once for I/O counters
            string? diskstatsContent = null;
            try { diskstatsContent = await ReadFileAsync("/proc/diskstats", ct); }
            catch { /* ignore */ }

            const int sectorSize = 512;

            foreach (var dev in devices)
            {
                var device = new StorageDeviceStats { Name = dev };
                var basePath = $"/sys/block/{dev}";

                // Size (sectors of 512 bytes)
                var sizeStr = await TryReadFileAsync($"{basePath}/size", ct);
                if (long.TryParse(sizeStr?.Trim(), out var sectors))
                    device.SizeBytes = sectors * sectorSize;

                // Rotational?
                var rot = await TryReadFileAsync($"{basePath}/queue/rotational", ct);
                device.IsRotational = rot?.Trim() == "1";

                // Model
                var model = await TryReadFileAsync($"{basePath}/device/model", ct);
                device.Model = model?.Trim() ?? string.Empty;

                // I/O stats from /proc/diskstats
                if (diskstatsContent != null)
                {
                    var match = Regex.Match(diskstatsContent,
                        $@"^\s*\d+\s+\d+\s+{Regex.Escape(dev)}\s+(\d+)\s+\d+\s+(\d+)\s+\d+\s+(\d+)\s+\d+\s+(\d+)",
                        RegexOptions.Multiline);
                    if (match.Success)
                    {
                        var reads = long.Parse(match.Groups[1].Value);
                        var sectorsRead = long.Parse(match.Groups[2].Value);
                        var writes = long.Parse(match.Groups[3].Value);
                        var sectorsWritten = long.Parse(match.Groups[4].Value);

                        device.BytesRead = sectorsRead * sectorSize;
                        device.BytesWritten = sectorsWritten * sectorSize;

                        if (elapsed > 0 && _prevDiskSamples.TryGetValue(dev, out var prev))
                        {
                            device.ReadBytesPerSec = Math.Max(0, (device.BytesRead - prev.BytesRead) / elapsed);
                            device.WriteBytesPerSec = Math.Max(0, (device.BytesWritten - prev.BytesWritten) / elapsed);
                            device.ReadsPerSec = Math.Max(0, (reads - prev.Reads) / elapsed);
                            device.WritesPerSec = Math.Max(0, (writes - prev.Writes) / elapsed);
                        }

                        _prevDiskSamples[dev] = new DiskSample
                        {
                            Reads = reads,
                            Writes = writes,
                            BytesRead = device.BytesRead,
                            BytesWritten = device.BytesWritten
                        };
                    }
                }

                // Drive temperature (best-effort via hwmon)
                device.TemperatureCelsius = await TryReadDriveTemperatureAsync(dev, ct);

                if (device.SizeBytes > 0)
                    results.Add(device);
            }

            results = results.OrderByDescending(d => d.SizeBytes).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect storage device stats");
        }
        return results;
    }

    private async Task<double?> TryReadDriveTemperatureAsync(string device, CancellationToken ct)
    {
        try
        {
            // 1. NVMe: /sys/class/nvme/.../hwmon*/temp1_input or via nvme cli
            // 2. SATA: smartctl -A /dev/<device>
            var path = $"/sys/block/{device}/device/hwmon";
            var result = await _shell.ExecuteAsync(
                $"cat {path}/hwmon*/temp1_input 2>/dev/null | head -1",
                timeoutMs: 2000, cancellationToken: ct);
            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                if (long.TryParse(result.StandardOutput.Trim(), out var milliC))
                    return milliC / 1000.0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read temperature for {Device}", device);
        }
        return null;
    }

    // ------------------------------ General info ------------------------------

    private async Task<(long uptime, string hostname, string kernel)>
        CollectGeneralInfoAsync(CancellationToken ct)
    {
        long uptime = 0;
        string hostname = string.Empty;
        string kernel = string.Empty;

        try
        {
            var content = await ReadFileAsync("/proc/uptime", ct);
            var first = content.Split(' ').FirstOrDefault();
            if (double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var u))
                uptime = (long)u;
        }
        catch { }

        try
        {
            hostname = (await TryReadFileAsync("/proc/sys/kernel/hostname", ct))?.Trim() ?? string.Empty;
        }
        catch { }

        try
        {
            kernel = (await TryReadFileAsync("/proc/sys/kernel/osrelease", ct))?.Trim() ?? string.Empty;
        }
        catch { }

        return (uptime, hostname, kernel);
    }

    // ------------------------------ Helpers ------------------------------

    private async Task<string> ReadFileAsync(string path, CancellationToken ct)
    {
        return await File.ReadAllTextAsync(path, ct);
    }

    private async Task<string?> TryReadFileAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return await File.ReadAllTextAsync(path, ct);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, long> ParseKeyValueKb(string content)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in content.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var key = line.Substring(0, colon).Trim();
            var value = line.Substring(colon + 1).Trim();
            // Strip trailing " kB"
            var sp = value.IndexOf(' ');
            if (sp > 0) value = value.Substring(0, sp);
            if (long.TryParse(value, out var v)) result[key] = v;
        }
        return result;
    }

    private static long GetKbAsBytes(Dictionary<string, long> dict, string key) =>
        dict.TryGetValue(key, out var v) ? v * 1024 : 0;

    private static long ParseLong(string s) =>
        long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static double ParseDouble(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static double Clamp(double v) => Math.Max(0, Math.Min(100, v));

    private class CpuSample
    {
        public long User;
        public long Nice;
        public long System;
        public long Idle;
        public long IoWait;
        public long Irq;
        public long SoftIrq;
        public long Steal;
        public long Total => User + Nice + System + Idle + IoWait + Irq + SoftIrq + Steal;
    }

    private class NetSample
    {
        public long BytesReceived;
        public long BytesSent;
    }

    private class DiskSample
    {
        public long Reads;
        public long Writes;
        public long BytesRead;
        public long BytesWritten;
    }
}
