using Microsoft.AspNetCore.Mvc;
using DellFanControl.Services;

namespace DellFanControl.Controllers;

public class StatusController : Controller
{
    private readonly FanControlService _fanControlService;
    private readonly FanStatusLogger _statusLogger;
    private readonly ILogger<StatusController> _logger;

    public StatusController(
        FanControlService fanControlService,
        FanStatusLogger statusLogger,
        ILogger<StatusController> logger)
    {
        _fanControlService = fanControlService;
        _statusLogger = statusLogger;
        _logger = logger;
    }

    // GET / - Main status page
    public IActionResult Index()
    {
        return View();
    }

    // GET /api/status - JSON API endpoint for current status
    [HttpGet("api/status")]
    public IActionResult GetStatus()
    {
        var status = _statusLogger.GetCurrentStatus();
        return Json(new 
        {
            timestamp = status.LastUpdateTime,
            mode = status.FanControlMode,
            temperatures = new
            {
                highest = status.TemperatureStatus.HighestTempCelsius,
                all = status.TemperatureStatus.AllTemperatures
            },
            fans = new
            {
                count = status.FanStatus.Fans.Count,
                averageRPM = status.FanStatus.AverageRPM,
                fans = status.FanStatus.Fans.Select(f => new
                {
                    name = f.Name,
                    rpm = f.RPM
                })
            }
        });
    }

    // POST /api/control/manual - Set manual fan speed
    [HttpPost("api/control/manual")]
    public async Task<IActionResult> SetManualSpeed([FromBody] ManualSpeedRequest request)
    {
        if (request == null || request.Speed < 0 || request.Speed > 100)
        {
            return BadRequest(new { error = "Speed must be between 0 and 100" });
        }

        try
        {
            await _fanControlService.SetManualFanSpeedAsync(request.Speed);
            return Ok(new { success = true, speed = request.Speed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set manual fan speed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // POST /api/control/dynamic - Restore dynamic control
    [HttpPost("api/control/dynamic")]
    public async Task<IActionResult> RestoreDynamic()
    {
        try
        {
            await _fanControlService.RestoreDynamicControlAsync();
            return Ok(new { success = true, message = "Dynamic control restored" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore dynamic control");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class ManualSpeedRequest
{
    public int Speed { get; set; }
}