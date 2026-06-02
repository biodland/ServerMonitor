using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerMonitor.Core.Enums;
using ServerMonitor.Core.Interfaces;
using ServerMonitor.Core.Models;

namespace ServerMonitor.Infrastructure.Detection;

/// <summary>
/// Marker for provider candidates. Concrete provider classes are registered
/// against this interface so they can be enumerated by the factory without
/// colliding with the single active <see cref="IServerProvider"/> registration.
/// </summary>
public interface IServerProviderCandidate : IServerProvider { }

/// <summary>
/// Default implementation of the server provider factory.
/// Detects the server hardware and returns the best matching provider.
/// </summary>
public class ServerProviderFactory : IServerProviderFactory
{
    private readonly IServerDetectionService _detection;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerProviderFactory> _logger;
    private readonly IEnumerable<IServerProviderCandidate> _candidates;

    public ServerProviderFactory(
        IServerDetectionService detection,
        IConfiguration configuration,
        ILogger<ServerProviderFactory> logger,
        IEnumerable<IServerProviderCandidate> candidates)
    {
        _detection = detection;
        _configuration = configuration;
        _logger = logger;
        _candidates = candidates;
    }

    public IEnumerable<IServerProvider> GetAvailableProviders() => _candidates;

    public async Task<IServerProvider> CreateProviderAsync(CancellationToken cancellationToken = default)
    {
        // Allow forcing a provider via config
        var forced = _configuration["ServerMonitor:ForceProvider"];
        if (!string.IsNullOrEmpty(forced))
        {
            var match = _candidates.FirstOrDefault(p =>
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
        var specific = _candidates
            .Where(p => p.IsSupported(info) && !p.Model.Equals("Generic", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (specific != null)
        {
            _logger.LogInformation("Selected provider: {Provider}", specific.DisplayName);
            await specific.InitializeAsync(cancellationToken);
            return specific;
        }

        // Fall back to a generic provider matching the vendor
        var generic = _candidates
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
