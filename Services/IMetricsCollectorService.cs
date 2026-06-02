using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Services;

/// <summary>
/// Provides cached, regularly-updated server metrics
/// </summary>
public interface IMetricsCollectorService
{
    /// <summary>
    /// Get the latest cached metrics snapshot
    /// </summary>
    ServerMetrics? Latest { get; }

    /// <summary>
    /// Get metrics history for graphing (oldest first)
    /// </summary>
    IReadOnlyList<ServerMetrics> History { get; }

    /// <summary>
    /// Force an immediate metrics refresh
    /// </summary>
    Task<ServerMetrics> RefreshAsync(CancellationToken cancellationToken = default);
}
