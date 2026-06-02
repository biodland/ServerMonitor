using Microsoft.AspNetCore.Mvc;
using ServerMonitor.Services;

namespace ServerMonitor.Controllers;

/// <summary>
/// Renders the system stats page (CPU / memory / network / storage)
/// </summary>
public class StatsController : Controller
{
    private readonly ISystemStatsService _stats;

    public StatsController(ISystemStatsService stats)
    {
        _stats = stats;
    }

    public IActionResult Index()
    {
        return View(_stats.Latest);
    }
}
