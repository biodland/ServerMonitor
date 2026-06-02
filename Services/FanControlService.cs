using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;

namespace ServerMonitor.Services;

/// <summary>
/// Background service that controls fan behavior based on configured policy.
/// Currently only supports manual fixed-speed mode (with periodic re-application
/// since some BMCs revert after a watchdog timeout).
/// 
/// Future: thermal-curve based control using metrics history.
/// </summary>
public class FanControlService : BackgroundService
{
    private readonly ILogger<FanControlService> _logger;
    private readonly IServerProvider _provider;
    private readonly IConfiguration _configuration;

    public FanControlService(
        ILogger<FanControlService> logger,
        IServerProvider provider,
        IConfiguration configuration)
    {
        _logger = logger;
        _provider = provider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("ServerMonitor:FanControl:EnableControl",
                       _configuration.GetValue<bool>("FanControl:EnableControl", false));

        if (!enabled)
        {
            _logger.LogInformation("FanControlService disabled by configuration");
            return;
        }

        if (!_provider.FanController.SupportsManualControl)
        {
            _logger.LogWarning("Provider {Provider} does not support manual fan control",
                _provider.DisplayName);
            return;
        }

        var intervalSeconds = _configuration.GetValue<int>("ServerMonitor:FanControl:CheckIntervalSeconds",
                              _configuration.GetValue<int>("FanControl:CheckIntervalSeconds", 30));

        var manualSpeed = _configuration.GetValue<int>("ServerMonitor:FanControl:ManualSpeed", 20);

        _logger.LogInformation(
            "FanControlService started (mode: manual, speed: {Speed}%, reapply every {Interval}s)",
            manualSpeed, intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _provider.FanController.SetFanSpeedAsync(manualSpeed, stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply fan speed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("FanControlService stopping - restoring automatic fan control");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _provider.FanController.RestoreAutoControlAsync(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore auto fan control on shutdown");
        }
    }
}
