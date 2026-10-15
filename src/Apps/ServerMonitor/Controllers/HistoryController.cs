using Microsoft.AspNetCore.Mvc;
using ServerMonitor.Infrastructure.Storage;

namespace ServerMonitor.Controllers;

/// <summary>
/// Renders the historical trends page (SQLite-backed charts + CSV export links).
/// All data is fetched client-side from the /api/history endpoints.
/// </summary>
public class HistoryController : Controller
{
    private readonly IHistoryStore _history;

    public HistoryController(IHistoryStore history)
    {
        _history = history;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.StorageAvailable = _history.IsAvailable;
        if (_history.IsAvailable)
        {
            var (metrics, stats) = await _history.GetCountsAsync(cancellationToken);
            ViewBag.MetricRows = metrics;
            ViewBag.StatRows = stats;
        }
        return View();
    }
}
