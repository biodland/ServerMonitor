using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Infrastructure.Detection;

/// <summary>
/// Default implementation of the server provider factory
/// Detects the server hardware and returns the best matching provider
/// </summary>
public class ServerProviderFactory : IServerProviderFactory
{
    private readonly IServerDetectionService _detection;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerProviderFactory> _logger;
    private readonly IEnumerable<IServerProvider> _providers;

    public ServerProviderFactory(
        IServerDetectionService detection,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ServerProviderFactory> logger,
        IEnumerable<IServerProvider> providers)
    {
        _detection = detection;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _providers = providers;
    }

    public IEnumerable<IServerProvider> GetAvailableProviders() => _providers;

    public async Task<IServerProvider> CreateProviderAsync(CancellationToken cancellationToken = default)
    {
        // Allow forcing a provider via config
        var forced = _configuration["ServerMonitor:ForceProvider"];
        if (!string.IsNullOrEmpty(forced))
        {
            var match = _providers.FirstOrDefault(p =>
                string.Equals(p.Model, forced, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.DisplayName, forced, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                _logger.LogInformation("Using forced provider: {Provider}", match.DisplayName);
                await match.InitializeAsync(cancellationToken);
                return match;
            }

            _logger.LogWarning("Forced provider '{Forced}' not found, falling back to detection", forced);
        }

        var info = await _detection.DetectServerAsync(cancellationToken);
        _logger.LogInformation("Detected server: {Vendor} {Model}", info.Vendor, info.Model);

        // Find the most specific matching provider (non-generic) first
        var specific = _providers
            .Where(p => p.IsSupported(info) && !p.Model.Equals("Generic", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (specific != null)
        {
            _logger.LogInformation("Selected provider: {Provider}", specific.DisplayName);
            await specific.InitializeAsync(cancellationToken);
            return specific;
        }

        // Fall back to a generic provider matching the vendor
        var generic = _providers
            .Where(p => p.IsSupported(info))
            .FirstOrDefault();

        if (generic != null)
        {
            _logger.LogInformation("Selected fallback provider: {Provider}", generic.DisplayName);
            await generic.InitializeAsync(cancellationToken);
            return generic;
        }

        _logger.LogError("No supported provider found for {Vendor} {Model}", info.Vendor, info.Model);
        throw new InvalidOperationException(
            $"No supported provider for {info.Vendor} {info.Model}");
    }
}
