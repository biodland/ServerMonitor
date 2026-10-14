using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Infrastructure.Storage;

/// <summary>
/// SQLite-backed <see cref="IHistoryStore"/> using Microsoft.Data.Sqlite.
/// Schema: one row per snapshot with indexed unix-timestamp plus chart-ready
/// columns; the full snapshot is kept as a JSON payload for future
/// detail views. WAL mode + short-lived connections allow concurrent
/// API reads while the background writer appends.
/// </summary>
public class SqliteHistoryStore : IHistoryStore
{
    private readonly ILogger<SqliteHistoryStore> _logger;
    private readonly HistoryStoreOptions _options;
    private readonly string _connectionString;
    private bool _available;

    private static readonly object BatteriesLock = new();
    private static bool _batteriesInitialized;

    /// <summary>Absolute database file path (useful for diagnostics/tests).</summary>
    public string FullPath { get; }

    public bool IsAvailable => _available && _options.Enabled;

    public SqliteHistoryStore(IConfiguration configuration, ILogger<SqliteHistoryStore> logger)
    {
        _logger = logger;
        _options = new HistoryStoreOptions();
        configuration.GetSection("ServerMonitor:Storage").Bind(_options);

        _options.RetentionDays = Math.Clamp(_options.RetentionDays, 1, 3650);
        _options.WriteIntervalSeconds = Math.Clamp(_options.WriteIntervalSeconds, 5, 3600);
        _options.MaxQueryPoints = Math.Clamp(_options.MaxQueryPoints, 10, 100000);

        var path = string.IsNullOrWhiteSpace(_options.Path) ? "data/history.db" : _options.Path;
        if (!Path.IsPathRooted(path))
            path = Path.Combine(AppContext.BaseDirectory, path);
        FullPath = Path.GetFullPath(path);
        _connectionString = $"Data Source={FullPath}";
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SQLite history storage is disabled by configuration");
            return;
        }

        try
        {
            EnsureBatteries();

            var dir = Path.GetDirectoryName(FullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await ExecuteAsync(conn,
                "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL;",
                cancellationToken);

            // Fresh database: enable incremental auto-vacuum before creating
            // tables so retention prunes actually release file space.
            var existing = Convert.ToInt64(await ScalarAsync(conn,
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('metric_snapshots','stat_snapshots');",
                cancellationToken));
            if (existing == 0)
                await ExecuteAsync(conn, "PRAGMA auto_vacuum=INCREMENTAL;", cancellationToken);

            await ExecuteAsync(conn, """
                CREATE TABLE IF NOT EXISTS metric_snapshots (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  ts INTEGER NOT NULL,
                  max_temp REAL NOT NULL DEFAULT 0,
                  avg_rpm INTEGER NOT NULL DEFAULT 0,
                  power_watts REAL NOT NULL DEFAULT 0,
                  health TEXT NOT NULL DEFAULT 'Good',
                  payload TEXT NOT NULL DEFAULT ''
                );
                CREATE INDEX IF NOT EXISTS ix_metric_snapshots_ts ON metric_snapshots(ts);
                CREATE TABLE IF NOT EXISTS stat_snapshots (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  ts INTEGER NOT NULL,
                  cpu REAL NOT NULL DEFAULT 0,
                  mem REAL NOT NULL DEFAULT 0,
                  net_rx REAL NOT NULL DEFAULT 0,
                  net_tx REAL NOT NULL DEFAULT 0,
                  disk_r REAL NOT NULL DEFAULT 0,
                  disk_w REAL NOT NULL DEFAULT 0,
                  payload TEXT NOT NULL DEFAULT ''
                );
                CREATE INDEX IF NOT EXISTS ix_stat_snapshots_ts ON stat_snapshots(ts);
                """, cancellationToken);

            _available = true;
            var (metrics, stats) = await GetCountsAsync(cancellationToken);
            _logger.LogInformation(
                "SQLite history store ready at {Path} (metrics: {Metrics}, stats: {Stats}, retention: {Retention}d)",
                FullPath, metrics, stats, _options.RetentionDays);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _available = false;
            _logger.LogError(ex,
                "Failed to initialize SQLite history store at {Path}; continuing with in-memory history only",
                FullPath);
        }
    }

    public async Task AppendMetricsAsync(ServerMetrics metrics, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return;
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO metric_snapshots (ts, max_temp, avg_rpm, power_watts, health, payload)
                VALUES (@ts, @max, @rpm, @watts, @health, @payload);
                """;
            cmd.Parameters.AddWithValue("@ts", ToUnix(metrics.Timestamp));
            cmd.Parameters.AddWithValue("@max", metrics.MaxTemperature);
            cmd.Parameters.AddWithValue("@rpm", metrics.AverageRpm);
            cmd.Parameters.AddWithValue("@watts", metrics.Power?.TotalWatts ?? 0);
            cmd.Parameters.AddWithValue("@health", metrics.OverallHealth.ToString());
            cmd.Parameters.AddWithValue("@payload", JsonSerializer.Serialize(metrics));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist metrics snapshot");
        }
    }

    public async Task AppendStatsAsync(SystemStats stats, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return;
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO stat_snapshots (ts, cpu, mem, net_rx, net_tx, disk_r, disk_w, payload)
                VALUES (@ts, @cpu, @mem, @rx, @tx, @dr, @dw, @payload);
                """;
            cmd.Parameters.AddWithValue("@ts", ToUnix(stats.Timestamp));
            cmd.Parameters.AddWithValue("@cpu", stats.Cpu.UsagePercent);
            cmd.Parameters.AddWithValue("@mem", stats.Memory.UsagePercent);
            cmd.Parameters.AddWithValue("@rx", stats.NetworkInterfaces.Sum(i => i.ReceiveBytesPerSec));
            cmd.Parameters.AddWithValue("@tx", stats.NetworkInterfaces.Sum(i => i.TransmitBytesPerSec));
            cmd.Parameters.AddWithValue("@dr", stats.StorageDevices.Sum(d => d.ReadBytesPerSec));
            cmd.Parameters.AddWithValue("@dw", stats.StorageDevices.Sum(d => d.WriteBytesPerSec));
            cmd.Parameters.AddWithValue("@payload", JsonSerializer.Serialize(stats));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist system stats snapshot");
        }
    }

    public async Task<IReadOnlyList<MetricHistoryPoint>> GetMetricHistoryAsync(
        DateTime from, DateTime to, int maxPoints, CancellationToken cancellationToken = default)
    {
        var points = new List<MetricHistoryPoint>();
        if (!IsAvailable) return points;
        maxPoints = Math.Clamp(maxPoints, 1, _options.MaxQueryPoints);

        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            // Even stride over the range (ROW_NUMBER keeps order stable across
            // prunes where ids are not contiguous); always keep the newest row.
            cmd.CommandText = """
                SELECT ts, max_temp, avg_rpm, power_watts, health FROM (
                  SELECT ts, max_temp, avg_rpm, power_watts, health,
                         ROW_NUMBER() OVER (ORDER BY ts) AS rn,
                         COUNT(*) OVER () AS cnt
                  FROM metric_snapshots WHERE ts BETWEEN @from AND @to
                ) WHERE ((rn - 1) % MAX(1, (cnt + @max - 1) / @max)) = 0 OR rn = cnt
                ORDER BY ts;
                """;
            cmd.Parameters.AddWithValue("@from", ToUnix(from));
            cmd.Parameters.AddWithValue("@to", ToUnix(to));
            cmd.Parameters.AddWithValue("@max", maxPoints);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                points.Add(new MetricHistoryPoint(
                    FromUnix(reader.GetInt64(0)),
                    reader.GetDouble(1),
                    reader.GetInt32(2),
                    reader.GetDouble(3),
                    reader.GetString(4)));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query metrics history");
        }
        return points;
    }

    public async Task<IReadOnlyList<StatHistoryPoint>> GetStatHistoryAsync(
        DateTime from, DateTime to, int maxPoints, CancellationToken cancellationToken = default)
    {
        var points = new List<StatHistoryPoint>();
        if (!IsAvailable) return points;
        maxPoints = Math.Clamp(maxPoints, 1, _options.MaxQueryPoints);

        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT ts, cpu, mem, net_rx, net_tx, disk_r, disk_w FROM (
                  SELECT ts, cpu, mem, net_rx, net_tx, disk_r, disk_w,
                         ROW_NUMBER() OVER (ORDER BY ts) AS rn,
                         COUNT(*) OVER () AS cnt
                  FROM stat_snapshots WHERE ts BETWEEN @from AND @to
                ) WHERE ((rn - 1) % MAX(1, (cnt + @max - 1) / @max)) = 0 OR rn = cnt
                ORDER BY ts;
                """;
            cmd.Parameters.AddWithValue("@from", ToUnix(from));
            cmd.Parameters.AddWithValue("@to", ToUnix(to));
            cmd.Parameters.AddWithValue("@max", maxPoints);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                points.Add(new StatHistoryPoint(
                    FromUnix(reader.GetInt64(0)),
                    reader.GetDouble(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.GetDouble(4),
                    reader.GetDouble(5),
                    reader.GetDouble(6)));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query system stats history");
        }
        return points;
    }

    public async Task<(long Metrics, long Stats)> GetCountsAsync(CancellationToken cancellationToken = default)
    {
        if (!_available) return (0, 0);
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            var m = Convert.ToInt64(await ScalarAsync(conn,
                "SELECT COUNT(*) FROM metric_snapshots;", cancellationToken));
            var s = Convert.ToInt64(await ScalarAsync(conn,
                "SELECT COUNT(*) FROM stat_snapshots;", cancellationToken));
            return (m, s);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count history rows");
            return (0, 0);
        }
    }

    public async Task<long> PruneAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return 0;
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            var cutoff = ToUnix(olderThan);

            await using var mDel = conn.CreateCommand();
            mDel.CommandText = "DELETE FROM metric_snapshots WHERE ts < @cut;";
            mDel.Parameters.AddWithValue("@cut", cutoff);
            var removedMetrics = await mDel.ExecuteNonQueryAsync(cancellationToken);

            await using var sDel = conn.CreateCommand();
            sDel.CommandText = "DELETE FROM stat_snapshots WHERE ts < @cut;";
            sDel.Parameters.AddWithValue("@cut", cutoff);
            var removedStats = await sDel.ExecuteNonQueryAsync(cancellationToken);

            var removed = (long)removedMetrics + removedStats;
            if (removed > 0)
            {
                try { await ExecuteAsync(conn, "PRAGMA incremental_vacuum;", cancellationToken); }
                catch (Exception ex) { _logger.LogDebug(ex, "incremental_vacuum skipped"); }
            }
            return removed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prune history");
            return 0;
        }
    }

    private static void EnsureBatteries()
    {
        if (_batteriesInitialized) return;
        lock (BatteriesLock)
        {
            if (_batteriesInitialized) return;
            // Required for the Core package (the meta-package does this via MSBuild);
            // idempotent, safe to call once per process.
            SQLitePCL.Batteries_V2.Init();
            _batteriesInitialized = true;
        }
    }

    private static async Task ExecuteAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<object?> ScalarAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync(ct);
    }

    private static long ToUnix(DateTime dt) =>
        new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

    private static DateTime FromUnix(long seconds) =>
        DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
}
