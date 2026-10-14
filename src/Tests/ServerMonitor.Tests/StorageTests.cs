using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ServerMonitor.Core.Models;
using ServerMonitor.Infrastructure.Storage;
using Xunit;

namespace ServerMonitor.Tests;

/// <summary>
/// SQLite history store round-trip tests. Each test uses an isolated temp
/// database file; connection pools are cleared so cleanup never hits a lock.
/// </summary>
public class StorageTests : IDisposable
{
    private readonly List<string> _dirs = new();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var dir in _dirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
        GC.SuppressFinalize(this);
    }

    private SqliteHistoryStore CreateStore(bool enabled = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sm-tests", Guid.NewGuid().ToString("N"));
        _dirs.Add(dir);
        var settings = new Dictionary<string, string?>
        {
            ["ServerMonitor:Storage:Enabled"] = enabled.ToString(),
            ["ServerMonitor:Storage:Path"] = Path.Combine(dir, "history.db"),
            ["ServerMonitor:Storage:RetentionDays"] = "30",
            ["ServerMonitor:Storage:WriteIntervalSeconds"] = "60",
            ["ServerMonitor:Storage:MaxQueryPoints"] = "5000",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new SqliteHistoryStore(config, NullLogger<SqliteHistoryStore>.Instance);
    }

    private static ServerMetrics MetricsAt(DateTime ts, double temp, int rpm, double watts) =>
        new()
        {
            Timestamp = ts,
            Temperatures = new List<TemperatureReading>
            {
                new() { Name = "Inlet", Value = temp }
            },
            Fans = new List<FanReading>
            {
                new() { Name = "Fan1", RPM = rpm }
            },
            Power = new PowerReading { TotalWatts = watts, IsAvailable = true },
        };

    private static SystemStats StatsAt(DateTime ts, double cpu, double mem) =>
        new()
        {
            Timestamp = ts,
            Cpu = new CpuStats { UsagePercent = cpu },
            Memory = new MemoryStats { TotalBytes = 1000, UsedBytes = (long)(10 * mem) },
            NetworkInterfaces = new List<NetworkInterfaceStats>
            {
                new() { Name = "eth0", ReceiveBytesPerSec = 100, TransmitBytesPerSec = 50 }
            },
            StorageDevices = new List<StorageDeviceStats>
            {
                new() { Name = "sda", ReadBytesPerSec = 1000, WriteBytesPerSec = 500 }
            },
        };

    [Fact]
    public async Task AppendAndQueryMetrics_RoundTripsInOrder()
    {
        var store = CreateStore();
        await store.InitializeAsync();
        Assert.True(store.IsAvailable);

        var base_ = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
            await store.AppendMetricsAsync(MetricsAt(base_.AddMinutes(i), 40 + i, 5000 + i * 10, 200 + i));

        var points = await store.GetMetricHistoryAsync(base_.AddMinutes(-1), base_.AddHours(1), 100);

        Assert.Equal(5, points.Count);
        Assert.Equal(40, points[0].MaxTemp);
        Assert.Equal(44, points[4].MaxTemp);
        Assert.Equal(5040, points[4].AvgRpm);
        Assert.Equal(204, points[4].PowerWatts);
        Assert.Equal("Good", points[0].Health);
        Assert.True(points.Zip(points.Skip(1), (a, b) => a.Timestamp <= b.Timestamp).All(x => x));

        var (m, s) = await store.GetCountsAsync();
        Assert.Equal(5, m);
        Assert.Equal(0, s);
    }

    [Fact]
    public async Task QueryDownsamples_LargeRangeKeepsFirstAndLast()
    {
        var store = CreateStore();
        await store.InitializeAsync();

        var base_ = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 100; i++)
            await store.AppendMetricsAsync(MetricsAt(base_.AddMinutes(i), 40, 5000, 200));

        var points = await store.GetMetricHistoryAsync(base_, base_.AddHours(2), 10);

        // Stride rows plus the guaranteed newest row.
        Assert.InRange(points.Count, 10, 11);
        Assert.Equal(base_, points[0].Timestamp);
        Assert.Equal(base_.AddMinutes(99), points[^1].Timestamp);
    }

    [Fact]
    public async Task Prune_RemovesOnlyOldRows()
    {
        var store = CreateStore();
        await store.InitializeAsync();

        var now = DateTime.UtcNow;
        await store.AppendMetricsAsync(MetricsAt(now.AddDays(-40), 40, 5000, 200));
        await store.AppendMetricsAsync(MetricsAt(now.AddDays(-10), 41, 5000, 200));
        await store.AppendStatsAsync(StatsAt(now.AddDays(-40), 10, 20));
        await store.AppendStatsAsync(StatsAt(now.AddDays(-1), 11, 21));

        var removed = await store.PruneAsync(now.AddDays(-30));

        Assert.Equal(2, removed);
        var (m, s) = await store.GetCountsAsync();
        Assert.Equal(1, m);
        Assert.Equal(1, s);
    }

    [Fact]
    public async Task AppendAndQueryStats_RoundTrips()
    {
        var store = CreateStore();
        await store.InitializeAsync();

        var base_ = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        await store.AppendStatsAsync(StatsAt(base_, 33.5, 40));

        var points = await store.GetStatHistoryAsync(base_.AddMinutes(-1), base_.AddHours(1), 100);

        var p = Assert.Single(points);
        Assert.Equal(33.5, p.CpuUsage);
        Assert.Equal(40, p.MemUsage);
        Assert.Equal(100, p.NetRx);
        Assert.Equal(50, p.NetTx);
        Assert.Equal(1000, p.DiskRead);
        Assert.Equal(500, p.DiskWrite);
    }

    [Fact]
    public async Task DisabledStore_IsNoOp()
    {
        var store = CreateStore(enabled: false);
        await store.InitializeAsync();

        Assert.False(store.IsAvailable);
        // Must never throw when unavailable.
        await store.AppendMetricsAsync(MetricsAt(DateTime.UtcNow, 40, 5000, 200));
        await store.AppendStatsAsync(StatsAt(DateTime.UtcNow, 10, 20));

        Assert.Empty(await store.GetMetricHistoryAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, 100));
        Assert.Empty(await store.GetStatHistoryAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, 100));
        Assert.Equal((0, 0), await store.GetCountsAsync());
        Assert.Equal(0, await store.PruneAsync(DateTime.UtcNow));
    }
}
