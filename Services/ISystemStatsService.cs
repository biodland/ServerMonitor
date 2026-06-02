using ServerMonitor.Core.Models;

namespace ServerMonitor.Services;

/// <summary>
/// Provides cached system statistics with rolling history for graphing
/// </summary>
public interface ISystemStatsService
{
    /// <summary>Latest system stats snapshot</summary>
    SystemStats? Latest { get; }

    /// <summary>Rolling history of stats snapshots (oldest first)</summary>
    IReadOnlyList<SystemStats> History { get; }

    /// <summary>Force an immediate refresh</summary>
    Task<SystemStats> RefreshAsync(CancellationToken cancellationToken = default);
}
