using ServerMonitor.Core.Models;

namespace ServerMonitor.Core.Interfaces;

/// <summary>
/// Provides system-level statistics (CPU, memory, network, storage).
/// Distinct from server hardware metrics (<see cref="IServerProvider"/>)
/// which deals with IPMI / fan / temperature sensor data.
/// </summary>
public interface ISystemStatsCollector
{
    /// <summary>
    /// Get a complete system statistics snapshot. Rate-based fields
    /// (network throughput, disk I/O) are computed using deltas from
    /// the previous call - the first call after process start will
    /// have zero rates.
    /// </summary>
    Task<SystemStats> CollectAsync(CancellationToken cancellationToken = default);
}
