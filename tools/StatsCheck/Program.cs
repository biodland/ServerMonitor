using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Models;
using ServerMonitor.Infrastructure.System;

// Simple verification harness for the LinuxSystemStatsCollector.
// Run via: dotnet run --project tools/StatsCheck/StatsCheck.csproj

using var lf = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
var shell = new ShellExecutor(lf.CreateLogger<ShellExecutor>());
var collector = new LinuxSystemStatsCollector(lf.CreateLogger<LinuxSystemStatsCollector>(), shell);

Console.WriteLine("--- Priming sample ---");
await collector.CollectAsync();
await Task.Delay(1500);

Console.WriteLine("--- Real sample ---");
var s = await collector.CollectAsync();

Console.WriteLine($"Hostname     : {s.Hostname}");
Console.WriteLine($"Kernel       : {s.KernelVersion}");
Console.WriteLine($"Uptime       : {s.UptimeSeconds}s");
Console.WriteLine();
Console.WriteLine($"CPU model    : {s.Cpu.ModelName}");
Console.WriteLine($"CPU usage    : {s.Cpu.UsagePercent:F1}%  (user {s.Cpu.UserPercent:F1}, sys {s.Cpu.SystemPercent:F1}, idle {s.Cpu.IdlePercent:F1}, iowait {s.Cpu.IoWaitPercent:F1})");
Console.WriteLine($"CPU cores    : {s.Cpu.CoreCount}  per-core: [{string.Join(", ", s.Cpu.PerCoreUsage.Select(x => x.ToString("F0")))}]");
Console.WriteLine($"Load avg     : {s.Cpu.LoadAverage1Min:F2} {s.Cpu.LoadAverage5Min:F2} {s.Cpu.LoadAverage15Min:F2}");
Console.WriteLine();
Console.WriteLine($"Memory total : {s.Memory.TotalBytes / 1024 / 1024} MiB");
Console.WriteLine($"Memory used  : {s.Memory.UsedBytes / 1024 / 1024} MiB ({s.Memory.UsagePercent:F1}%)");
Console.WriteLine($"Mem cached   : {s.Memory.CachedBytes / 1024 / 1024} MiB");
Console.WriteLine($"Swap         : {s.Memory.SwapUsedBytes / 1024 / 1024} / {s.Memory.SwapTotalBytes / 1024 / 1024} MiB");
Console.WriteLine();
Console.WriteLine($"Network interfaces: {s.NetworkInterfaces.Count}");
foreach (var ni in s.NetworkInterfaces.Take(8))
{
    Console.WriteLine($"  {ni.Name,-12} up={ni.IsUp} speed={ni.SpeedMbps}Mbps " +
                      $"rx={ni.ReceiveBytesPerSec/1024:F1}KB/s tx={ni.TransmitBytesPerSec/1024:F1}KB/s " +
                      $"rxTotal={ni.BytesReceived} ip={string.Join(",", ni.IpAddresses)}");
}
Console.WriteLine();
Console.WriteLine($"Volumes: {s.StorageVolumes.Count}");
foreach (var v in s.StorageVolumes.Take(8))
{
    Console.WriteLine($"  {v.MountPoint,-20} {v.FilesystemType,-8} " +
                      $"used={v.UsedBytes/1024/1024}MiB / {v.TotalBytes/1024/1024}MiB ({v.UsagePercent:F1}%)");
}
Console.WriteLine();
Console.WriteLine($"Devices: {s.StorageDevices.Count}");
foreach (var d in s.StorageDevices.Take(8))
{
    Console.WriteLine($"  {d.Name,-12} {(d.IsRotational?"HDD":"SSD")} {d.SizeBytes/1024/1024}MiB " +
                      $"r={d.ReadBytesPerSec/1024:F1}KB/s w={d.WriteBytesPerSec/1024:F1}KB/s " +
                      $"temp={(d.TemperatureCelsius?.ToString("F0") ?? "--")}°C  model={d.Model}");
}
