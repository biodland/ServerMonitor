using ServerMonitor.Core.Interfaces;
using ServerMonitor.Infrastructure.Detection;
using ServerMonitor.Infrastructure.Gpu;
using ServerMonitor.Infrastructure.Ipmi;
using ServerMonitor.Infrastructure.System;
using ServerMonitor.Providers.Dell;
using ServerMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Logging - configure early so DI-resolution diagnostics are visible
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// MVC
builder.Services.AddControllersWithViews();

// Infrastructure - System utilities
builder.Services.AddSingleton<ShellExecutor>();
builder.Services.AddSingleton<LmSensorsParser>();

// Infrastructure - IPMI
builder.Services.AddSingleton<IIpmiClient, IpmiToolClient>();

// Infrastructure - Detection
builder.Services.AddSingleton<IServerDetectionService, ServerDetectionService>();
builder.Services.AddSingleton<IServerProviderFactory, ServerProviderFactory>();

// Infrastructure - GPU
builder.Services.AddSingleton<IGpuMonitor, GpuMonitor>();

// Provider candidates - registered as IServerProviderCandidate so they can be
// enumerated by the factory without conflicting with the single active
// IServerProvider registration below.
// Specific providers are tried first; DellGenericProvider is the fallback.
builder.Services.AddSingleton<IServerProviderCandidate, DellR720XdProvider>();
builder.Services.AddSingleton<IServerProviderCandidate, DellR740XdProvider>();
builder.Services.AddSingleton<IServerProviderCandidate, DellR240Provider>();
builder.Services.AddSingleton<IServerProviderCandidate, DellGenericProvider>();
// SuperMicro / ASRock providers can be added here when implemented

// The active IServerProvider is selected once at application startup
// (before the host is built) and registered as a singleton instance.
// This avoids blocking on async work inside the DI container, which can
// cause silent hangs.

// Application services
builder.Services.AddSingleton<MetricsCollectorService>();
builder.Services.AddSingleton<IMetricsCollectorService>(sp => sp.GetRequiredService<MetricsCollectorService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MetricsCollectorService>());

builder.Services.AddHostedService<FanControlService>();

// Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    var port = builder.Configuration.GetValue<int?>("ServerMonitor:Server:Port")
               ?? builder.Configuration.GetValue<int>("Server:Port", 5000);
    options.ListenAnyIP(port);

    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║   ServerMonitor - Modular Server Monitoring & Fan Control        ║");
    Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║   Web Interface : http://localhost:{port,-30}║");
    Console.WriteLine($"║   API Endpoint  : http://localhost:{port}/api/status{new string(' ', Math.Max(0, 19 - port.ToString().Length))}║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
});

// ----- Resolve the active provider BEFORE building the WebApplication -----
// We instantiate the minimal pieces needed for detection manually (avoiding
// BuildServiceProvider, which would create duplicate singletons), then
// register the resulting IServerProvider instance for the full app.

Console.WriteLine("Detecting server hardware...");
IServerProvider activeProvider;
try
{
    using var loggerFactory = LoggerFactory.Create(b =>
    {
        b.AddConsole();
        b.SetMinimumLevel(LogLevel.Information);
    });

    var shell = new ShellExecutor(loggerFactory.CreateLogger<ShellExecutor>());
    var lmSensors = new LmSensorsParser(loggerFactory.CreateLogger<LmSensorsParser>(), shell);
    var ipmiClient = new IpmiToolClient(
        loggerFactory.CreateLogger<IpmiToolClient>(), shell, builder.Configuration);
    var detection = new ServerDetectionService(
        loggerFactory.CreateLogger<ServerDetectionService>(), shell);

    var candidates = new IServerProviderCandidate[]
    {
        new DellR720XdProvider(ipmiClient, lmSensors, loggerFactory),
        new DellR740XdProvider(ipmiClient, lmSensors, loggerFactory),
        new DellR240Provider(ipmiClient, lmSensors, loggerFactory),
        new DellGenericProvider(ipmiClient, lmSensors, loggerFactory),
    };

    var factory = new ServerProviderFactory(
        detection,
        builder.Configuration,
        loggerFactory.CreateLogger<ServerProviderFactory>(),
        candidates);

    // Use a generous timeout - some BMCs are slow to respond
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    activeProvider = await factory.CreateProviderAsync(cts.Token);

    Console.WriteLine($"Active provider: {activeProvider.DisplayName} " +
                      $"({activeProvider.Vendor} {activeProvider.Model})");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL: Failed to initialize server provider: {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
    Console.Error.WriteLine();
    Console.Error.WriteLine("Possible causes:");
    Console.Error.WriteLine("  - ipmitool is not installed (apt-get install ipmitool)");
    Console.Error.WriteLine("  - Insufficient permissions (try running with sudo)");
    Console.Error.WriteLine("  - DMI/SMBIOS access blocked (/sys/class/dmi/id/)");
    Console.Error.WriteLine("  - Hardware not yet supported (see DESIGN.md)");
    return 1;
}

// Register the resolved provider as a singleton instance
builder.Services.AddSingleton<IServerProvider>(activeProvider);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Dashboard/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllers(); // attribute-routed (e.g. ApiController)

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ServerMonitor starting (provider: {Provider})...", activeProvider.DisplayName);

// Test the hardware connection but don't block startup on it
_ = Task.Run(async () =>
{
    try
    {
        var connected = await activeProvider.TestConnectionAsync();
        if (connected)
            logger.LogInformation("Hardware connection: SUCCESS");
        else
            logger.LogWarning("Hardware connection: FAILED - check IPMI configuration / ipmitool installation");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Hardware connection test failed");
    }
});

await app.RunAsync();
return 0;
