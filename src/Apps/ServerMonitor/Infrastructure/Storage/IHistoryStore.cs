using ServerMonitor.Core.Models;

namespace ServerMonitor.Infrastructure.Storage;

/// <summary>
/// Persistent metrics history (DESIGN.md Phase 5).
/// Implementations must degrade gracefully: when unavailable,
/// appends are no-ops and queries return empty results so callers can
/// fall back to the in-memory rolling buffers.
/// </summary>
public interface IHistoryStore
{
    /// <summary>True once initialized and enabled.</summary>
    bool IsAvailable { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task AppendMetricsAsync(ServerMetrics metrics, CancellationToken cancellationToken = default);

    Task AppendStatsAsync(SystemStats stats, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetricHistoryPoint>> GetMetricHistoryAsync(
        DateTime from, DateTime to, int maxPoints, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatHistoryPoint>> GetStatHistoryAsync(
        DateTime from, DateTime to, int maxPoints, CancellationToken cancellationToken = default);

    Task<(long Metrics, long Stats)> GetCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Delete rows older than <paramref name="olderThan"/>. Returns rows removed.</summary>
    Task<long> PruneAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
