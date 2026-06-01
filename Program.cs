using DellFanControl.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Register our services
builder.Services.AddSingleton<IPMIService>();
builder.Services.AddSingleton<FanStatusLogger>();
builder.Services.AddHostedService<FanControlService>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    var port = builder.Configuration.GetValue<int>("Server:Port", 5000);
    options.ListenAnyIP(port);
    
    // Log the URL
    Console.WriteLine($"");
    Console.WriteLine($"╔═════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║   Dell R720 XD Fan Control System                       ║");
    Console.WriteLine($"╠═════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║   Web Interface: http://localhost:{port}                  ║");
    Console.WriteLine($"║   API Endpoint:    http://localhost:{port}/api/status     ║");
    Console.WriteLine($"╚═════════════════════════════════════════════════════════╝");
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

if (string.IsNullOrEmpty(idracPass))
{
    logger.LogWarning("iDRAC password not configured! Set Idrac:Password in appsettings.json");
}

logger.LogInformation("iDRAC Configuration: IP={IP}, User={User}", idracIp, idracUser);

app.Run();