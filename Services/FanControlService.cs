using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DellFanControl.Services;

public class FanControlService : BackgroundService
{
    private readonly ILogger<FanControlService> _logger;
    private readonly IPMIService _ipmiService;
    private readonly IConfiguration _configuration;
    private readonly FanStatusLogger _statusLogger;

    // Current system state
    public TemperatureStatus CurrentTemperature { get; private set; } = new();
    public FanStatus CurrentFanStatus { get; private set; } = new();
    public int CurrentFanSpeedPercentage { get; private set; } = -1;
    public string CurrentMode { get; private set; } = "Initializing";

    public FanControlService(
        ILogger<FanControlService> logger,
        IPMIService ipmiService,
        IConfiguration configuration,
        FanStatusLogger statusLogger)
    {
        _logger = logger;
        _ipmiService = ipmiService;
        _configuration = configuration;
        _statusLogger = statusLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Fan Control Service starting...");

        // Configuration values
        var checkIntervalSeconds = _configuration.GetValue<int>("FanControl:CheckIntervalSeconds", 30);
        bool enableControl = _configuration.GetValue<bool>("FanControl:EnableControl", true);

        _logger.LogInformation("Configuration: CheckInterval={Interval}s, ControlEnabled={Enabled}", 
            checkIntervalSeconds, enableControl);

        // Initial delay
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunControlCycleAsync(enableControl, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fan control cycle. Attempting to restore dynamic control...");
                
                try
                {
                    await _ipmiService.RestoreDynamicControlAsync(stoppingToken);
                    CurrentMode = "Dynamic (Error Recovery)";
                    CurrentFanSpeedPercentage = -1;
                    
                    // Log the error state
                    _statusLogger.LogStatus(CurrentTemperature, CurrentFanStatus, CurrentMode);
                }
                catch (Exception restoreEx)
                {
                    _logger.LogError(restoreEx, "Failed to restore dynamic control after error");
                }
            }

            // Wait for next cycle
            await Task.Delay(TimeSpan.FromSeconds(checkIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Fan Control Service stopping. Restoring dynamic control...");
        try
        {
            await _ipmiService.RestoreDynamicControlAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore dynamic control during shutdown");
        }
    }

    private async Task RunControlCycleAsync(bool enableControl, CancellationToken cancellationToken)
    {
        // Step 1: Read temperatures
        CurrentTemperature = await _ipmiService.GetTemperaturesAsync(cancellationToken);
        
        // Step 2: Read fan status
        CurrentFanStatus = await _ipmiService.GetFanStatusAsync(cancellationToken);
        
        // Step 3: Determine appropriate fan speed based on temperature
        int targetSpeed = CalculateTargetFanSpeed(CurrentTemperature.HighestTempCelsius);
        
        // Step 4: Apply fan speed if control is enabled
        if (enableControl && targetSpeed != CurrentFanSpeedPercentage)
        {
            await _ipmiService.SetFanSpeedAsync(targetSpeed, cancellationToken);
            CurrentFanSpeedPercentage = targetSpeed;
            CurrentMode = "Manual Control";
            
            _logger.LogInformation("Fan speed adjusted: {Speed}% (Max temp: {MaxTemp}°C)", 
                targetSpeed, CurrentTemperature.HighestTempCelsius);
        }
        else if (targetSpeed == -1)
        {
            // Temperature too high - restore dynamic control
            if (CurrentMode != "Dynamic (High Temp)")
            {
                await _ipmiService.RestoreDynamicControlAsync(cancellationToken);
                CurrentFanSpeedPercentage = -1;
                CurrentMode = "Dynamic (High Temp)";
                
                _logger.LogWarning("Temperature too high ({MaxTemp}°C). Restoring dynamic control.", 
                    CurrentTemperature.HighestTempCelsius);
            }
        }
        else if (!enableControl)
        {
            CurrentMode = "Monitoring Only (Control Disabled)";
        }
        
        // Step 5: Log current status
        _statusLogger.LogStatus(CurrentTemperature, CurrentFanStatus, CurrentMode);
    }

    /// <summary>
    /// Calculates target fan speed percentage based on temperature.
    /// Returns -1 if temperature is too high and dynamic control should be restored.
    /// </summary>
    private int CalculateTargetFanSpeed(int highestTemp)
    {
        // Temperature thresholds based on the bash script:
        // 0-35°C  -> 10%
        // 40-45°C -> 15%
        // 46-49°C -> 20%
        // 50-54°C -> 25%
        // 55+°C   -> Restore dynamic control
        
        return highestTemp switch
        {
            >= 0 and <= 39 => 10,  // 0-39°C (slightly merged range from original script)
            >= 40 and <= 45 => 15, // 40-45°C
            >= 46 and <= 49 => 20, // 46-49°C
            >= 50 and <= 54 => 25, // 50-54°C
            _ => -1                // 55°C or higher - restore dynamic control
        };
    }

    /// <summary>
    /// Manually trigger a fan speed adjustment (for API/Manual control)
    /// </summary>
    public async Task SetManualFanSpeedAsync(int percentage, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual fan speed request: {Percentage}%", percentage);
        
        await _ipmiService.SetFanSpeedAsync(percentage, cancellationToken);
        CurrentFanSpeedPercentage = percentage;
        CurrentMode = "Manual Override";
        
        _statusLogger.LogStatus(CurrentTemperature, CurrentFanStatus, CurrentMode);
    }

    /// <summary>
    /// Manually restore dynamic control
    /// </summary>
    public async Task RestoreDynamicControlAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual request to restore dynamic control");
        
        await _ipmiService.RestoreDynamicControlAsync(cancellationToken);
        CurrentFanSpeedPercentage = -1;
        CurrentMode = "Dynamic (Manual)";
        
        _statusLogger.LogStatus(CurrentTemperature, CurrentFanStatus, CurrentMode);
    }
}