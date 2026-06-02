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

// Providers - register all known providers
// Specific providers are tried first; DellGenericProvider is the fallback
builder.Services.AddSingleton<IServerProvider, DellR720XdProvider>();
builder.Services.AddSingleton<IServerProvider, DellR740XdProvider>();
builder.Services.AddSingleton<IServerProvider, DellR240Provider>();
builder.Services.AddSingleton<IServerProvider, DellGenericProvider>();
// SuperMicro / ASRock providers can be added here when implemented

// Active provider (resolved once at startup via factory)
builder.Services.AddSingleton<IServerProvider>(sp =>
{
    var factory = sp.GetRequiredService<IServerProviderFactory>();
    return factory.CreateProviderAsync().GetAwaiter().GetResult();
});

// Application services
builder.Services.AddSingleton<MetricsCollectorService>();
builder.Services.AddSingleton<IMetricsCollectorService>(sp => sp.GetRequiredService<MetricsCollectorService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MetricsCollectorService>());

builder.Services.AddHostedService<FanControlService>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

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

// Startup logging - resolve the provider so detection runs before serving requests
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ServerMonitor starting...");

try
{
    var provider = app.Services.GetRequiredService<IServerProvider>();
    logger.LogInformation("Active provider: {Provider} ({Vendor} {Model})",
        provider.DisplayName, provider.Vendor, provider.Model);

    var connected = await provider.TestConnectionAsync();
    if (connected)
        logger.LogInformation("Hardware connection: SUCCESS");
    else
        logger.LogWarning("Hardware connection: FAILED - check IPMI configuration / ipmitool installation");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize server provider: {Message}", ex.Message);
    logger.LogError("Ensure ipmitool is installed (apt-get install ipmitool) and your hardware is supported");
}

app.Run();
