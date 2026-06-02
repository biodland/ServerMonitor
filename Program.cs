using DellFanControl.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Determine which IPMI service to use
var useLocalIpmi = builder.Configuration.GetValue<bool>("Idrac:UseLocal", true);

// Register appropriate IPMI service
if (useLocalIpmi)
{
    builder.Services.AddSingleton<IIPMIService, IPMIService_Local>();
    Console.WriteLine("→ Using LOCAL in-band IPMI service");
}
else
{
    builder.Services.AddSingleton<IIPMIService, IPMIService>();
    Console.WriteLine("→ Using NETWORK-based IPMI service");
}

// Register our services
builder.Services.AddSingleton<FanStatusLogger>();
builder.Services.AddSingleton<GpuService>();
builder.Services.AddSingleton<FanControlService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FanControlService>());

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    var port = builder.Configuration.GetValue<int>("Server:Port", 1080);
    options.ListenAnyIP(port);
    
    // Log the URL
    Console.WriteLine($"");
    Console.WriteLine($"╔═══════════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║   Dell R720 XD Fan Control System                       ║");
    Console.WriteLine($"╠═══════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║   Web Interface: http://localhost:{port}                  ║");
    Console.WriteLine($"║   API Endpoint:    http://localhost:{port}/api/status     ║");
    Console.WriteLine($"╚═══════════════════════════════════════════════════════════════╝");
    Console.WriteLine($"");
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Status}/{action=Index}/{id?}");

// Startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Dell R720 XD Fan Control System starting...");

// Check configuration
var idracIp = builder.Configuration["Idrac:Ip"];
var idracUser = builder.Configuration["Idrac:User"];
var idracPass = builder.Configuration["Idrac:Password"];

if (useLocalIpmi)
{
    logger.LogInformation("Using local in-band IPMI (direct BMC access)");
    
    // Test local IPMI connection
    var ipmiService = app.Services.GetRequiredService<IIPMIService>();
    try
    {
        var testResult = await ipmiService.TestConnectionAsync();
        if (testResult)
        {
            logger.LogInformation("Local IPMI connection test: SUCCESS");
        }
        else
        {
            logger.LogWarning("Local IPMI connection test: FAILED - Check ipmitool access and permissions");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Local IPMI connection test: ERROR - {Message}", ex.Message);
        logger.LogError("Ensure ipmitool is installed: apt-get install ipmitool");
    }
}
else
{
    logger.LogInformation("iDRAC Configuration: IP={IP}, User={User}", idracIp, idracUser);
    
    if (string.IsNullOrEmpty(idracPass))
    {
        logger.LogWarning("iDRAC password not configured! Set Idrac:Password in appsettings.json");
    }
}

app.Run();