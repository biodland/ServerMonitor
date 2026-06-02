using Microsoft.AspNetCore.Mvc;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Services;

namespace ServerMonitor.Controllers;

/// <summary>
/// Renders the main HTML dashboard
/// </summary>
public class DashboardController : Controller
{
    private readonly IMetricsCollectorService _metrics;
    private readonly IServerProvider _provider;

    public DashboardController(
        IMetricsCollectorService metrics,
        IServerProvider provider)
    {
        _metrics = metrics;
        _provider = provider;
    }

    public IActionResult Index()
    {
        ViewBag.ProviderName = _provider.DisplayName;
        ViewBag.Vendor = _provider.Vendor.ToString();
        ViewBag.Model = _provider.Model;
        return View(_metrics.Latest);
    }
}
