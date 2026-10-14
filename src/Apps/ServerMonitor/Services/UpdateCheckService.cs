using System.Reflection;
using System.Text.Json;

namespace ServerMonitor.Services;

public sealed class UpdateCheckService : BackgroundService
{
    private const string AppName = "ServerMonitor";
    private readonly IConfiguration _configuration;
    private readonly ILogger<UpdateCheckService> _logger;

    public UpdateCheckService(
        IConfiguration configuration,
        ILogger<UpdateCheckService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var checkEnabled = _configuration.GetValue("ServerMonitor:Updates:CheckEnabled", true);
        var reportUsage = _configuration.GetValue("ServerMonitor:Updates:ReportUsage", false);
        if (!checkEnabled && !reportUsage)
            return;

        var version = GetCurrentVersion();
        var channel = _configuration.GetValue("ServerMonitor:Updates:Channel", "stable")!;

        if (reportUsage)
        {
            try
            {
                await OdlandUpdateCheck.ReportRunAsync(AppName, version, stoppingToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Could not report ServerMonitor run to the update service");
            }
            catch (UriFormatException ex)
            {
                _logger.LogWarning(ex, "Invalid update service URL; ServerMonitor run was not reported");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid update service request; ServerMonitor run was not reported");
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Timed out reporting ServerMonitor run to the update service");
            }
        }

        if (!checkEnabled || stoppingToken.IsCancellationRequested)
            return;

        try
        {
            var update = await OdlandUpdateCheck.CheckForUpdateAsync(
                AppName, version, channel, stoppingToken);
            if (update?.UpdateAvailable == true)
            {
                _logger.LogInformation(
                    "ServerMonitor update available: {CurrentVersion} -> {LatestVersion}. Download: {DownloadUrl}. Mandatory: {Mandatory}",
                    version, update.Latest, update.DownloadUrl, update.Mandatory);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not check for ServerMonitor updates");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Update service returned an invalid ServerMonitor update response");
        }
        catch (UriFormatException ex)
        {
            _logger.LogWarning(ex, "Invalid update service URL; ServerMonitor update check was skipped");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid update service request; ServerMonitor update check was skipped");
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timed out checking for ServerMonitor updates");
        }
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var informationalVersion = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataIndex = informationalVersion.IndexOf('+');
            return metadataIndex < 0 ? informationalVersion : informationalVersion[..metadataIndex];
        }

        return assembly?.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
