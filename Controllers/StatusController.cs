using Microsoft.AspNetCore.Mvc;
using DellFanControl.Services;

namespace DellFanControl.Controllers;

public class StatusController : Controller
{
    private readonly FanControlService _fanControlService;
    private readonly FanStatusLogger _statusLogger;
    private readonly GpuService _gpuService;
    private readonly IIPMIService _ipmiService;
    private readonly ILogger<StatusController> _logger;

    public StatusController(
        FanControlService fanControlService,
        FanStatusLogger statusLogger,
        GpuService gpuService,
        IIPMIService ipmiService,
        ILogger<StatusController> logger)
    {
        _fanControlService = fanControlService;
        _statusLogger = statusLogger;
        _gpuService = gpuService;
        _ipmiService = ipmiService;
        _logger = logger;
    }

    // GET / - Main status page
    public IActionResult Index()
    {
        return View();
    }

    // GET /api/status - JSON API endpoint for current status
    [HttpGet("api/status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = _statusLogger.GetCurrentStatus();
        
        // Get GPU temperatures
        var gpuTemps = await _gpuService.GetGpuTemperaturesAsync();
        
        // Get detailed sensor information
        var tempStatus = status.TemperatureStatus;
        var fanStatus = status.FanStatus;
        
        return Json(new 
        {
            timestamp = status.LastUpdateTime,
            mode = status.FanControlMode,
            temperatureStatus = new
            {
                highest = tempStatus.HighestTempCelsius,
                count = tempStatus.AllTemperatures.Count
            },
            fans = new
            {
                count = fanStatus.Fans.Count,
                averageRPM = fanStatus.AverageRPM,
                fans = fanStatus.Fans.Select(f => new
                {
                    name = f.Name,
                    rpm = f.RPM
                })
            },
            sensors = new
            {
                temperatures = await GetAllTemperatureReadingsAsync(),
                gpus = gpuTemps
            }
        });
    }

    /// <summary>
    /// Get all temperature readings from IPMI
    /// </summary>
    private async Task<List<TemperatureSensor>> GetAllTemperatureReadingsAsync()
    {
        var sensors = new List<TemperatureSensor>();
        
        try
        {
            var command = "ipmitool sensor | grep -E 'degrees C|Temp'";
            var output = await ExecuteCommandAsync(command);
            
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    var sensorName = parts[0].Trim();
                    var tempValue = parts[1].Trim();
                    
                    if (double.TryParse(tempValue, out var temp))
                    {
                        sensors.Add(new TemperatureSensor
                        {
                            Name = sensorName,
                            Temperature = (int)Math.Round(temp),
                            Unit = "°C"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting all temperature readings");
        }
        
        return sensors;
    }

    private Task<string> ExecuteCommandAsync(string command)
    {
        var tcs = new TaskCompletionSource<string>();

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    error.AppendLine(e.Data);
                }
            };

            process.Exited += (sender, e) =>
            {
                if (process.ExitCode == 0)
                {
                    tcs.SetResult(output.ToString());
                }
                else
                {
                    tcs.SetResult(string.Empty);
                }
                process.Dispose();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            tcs.SetResult(string.Empty);
        }

        return tcs.Task;
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

public class TemperatureSensor
{
    public string Name { get; init; } = string.Empty;
    public int Temperature { get; init; }
    public string Unit { get; init; } = string.Empty;
}