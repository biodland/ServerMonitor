namespace ServerMonitor.Infrastructure.Storage;

/// <summary>
/// Compact hardware-metrics point for trend charts (mirrors the
/// <c>/api/history</c> projection).
/// </summary>
public record MetricHistoryPoint(
    DateTime Timestamp,
    double MaxTemp,
    int AvgRpm,
    double PowerWatts,
    string Health);

/// <summary>
/// Compact system-stats point for trend charts (mirrors the
/// <c>/api/stats/history</c> projection).
/// </summary>
public record StatHistoryPoint(
    DateTime Timestamp,
    double CpuUsage,
    double MemUsage,
    double NetRx,
    double NetTx,
    double DiskRead,
    double DiskWrite);
