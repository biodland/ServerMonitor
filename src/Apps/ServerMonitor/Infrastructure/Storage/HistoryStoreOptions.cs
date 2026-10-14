namespace ServerMonitor.Infrastructure.Storage;

/// <summary>
/// Storage settings (section <c>ServerMonitor:Storage</c>).
/// Snapshots are written every <see cref="WriteIntervalSeconds"/> (decimated
/// from the 5s/2s live collectors) and rows older than
/// <see cref="RetentionDays"/> are pruned.
/// </summary>
public class HistoryStoreOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Database file path. Relative paths resolve against the app base directory.
    /// </summary>
    public string Path { get; set; } = "data/history.db";

    public int RetentionDays { get; set; } = 30;

    public int WriteIntervalSeconds { get; set; } = 60;

    /// <summary>Upper bound for points returned by a single history query.</summary>
    public int MaxQueryPoints { get; set; } = 5000;
}
