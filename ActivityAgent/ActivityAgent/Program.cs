using ActivityAgent.Configuration;
using ActivityTracker.Configuration;
using ActivityTracker.Data;
using ActivityTracker.Helpers;
using ActivityTracker.Logging;
using ActivityTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

var builder = Host.CreateApplicationBuilder(args);
bool isAgentMode = args.Contains("--agent");

// Use ProgramData for DB and logs — this is writable by Windows Services (SYSTEM account).
// LocalApplicationData resolves to C:\Windows\System32\config\systemprofile\AppData\Local
// when running as a service, which is often inaccessible or non-existent.
var appDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "ActivityMonitor"
);
Directory.CreateDirectory(appDataFolder);
var dbPath = Path.Combine(appDataFolder, "activity.db");

// Enable Windows Service
if (!isAgentMode)
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "ActivityMonitor";
    });
}

// ============================================================
// CONFIGURATION - Load from multiple locations
// ============================================================

// Path to appconfig.json in ProgramData (reuse appDataFolder created above)
var configFilePath = Path.Combine(appDataFolder, "appconfig.json");

// Create a default appconfig.json if it doesn't exist to prevent errors and guide settings
if (!File.Exists(configFilePath))
{
    var defaultConfig = """
        {
          "EmployeeId": "",
          "MachineId": ""
        }
        """;
    try
    {
        File.WriteAllText(configFilePath, defaultConfig);
    }
    catch
    {
        // Ignore errors creating default configuration, as we will use fallbacks and optional load
    }
}

// Load configuration
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    // Load appsettings.json from app folder (optional)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    // Load appconfig.json from ProgramData (optional)
    .AddJsonFile(configFilePath, optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// ============================================================
// REGISTER CONFIGURATION
// ============================================================

// Register AppConfig (from appconfig.json)
builder.Services.Configure<AppConfig>(
    builder.Configuration);

// Register AppSettings (from appsettings.json)
builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("AppSettings"));

// ============================================================
// DATABASE
// ============================================================

// SQLite
builder.Services.AddDbContext<TrackerDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Repository
builder.Services.AddScoped<IRepository, Repository>();

// ============================================================
// HOOKS & SERVICES
// ============================================================

// Hooks
builder.Services.AddSingleton<KeyboardHook>();
builder.Services.AddSingleton<MouseHook>();

// Core services
builder.Services.AddSingleton<ActivityService>();
builder.Services.AddSingleton<SystemEventService>();

// Background Workers
if (isAgentMode)
{
    builder.Services.AddHostedService<WindowTrackerService>();
    builder.Services.AddHostedService<IdleTrackerService>();
}
else
{
    builder.Services.AddHostedService<SessionAgentLauncherService>();
    builder.Services.AddHostedService<SyncService>();
}

// ============================================================
// HTTP CLIENT
// ============================================================

// HttpClient
builder.Services.AddHttpClient("ActivityApi")
    .ConfigureHttpClient((sp, client) =>
    {
        var settings = sp
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>()
            .Value;

        if (!string.IsNullOrWhiteSpace(settings.Api.BaseUrl))
            client.BaseAddress = new Uri(settings.Api.BaseUrl);

        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var settings = sp
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>()
            .Value;

        var handler = new HttpClientHandler();

        if (settings.Api.IgnoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    });

// ============================================================
// LOGGING
// ============================================================

// Configure file logging in ProgramData directory (accessible by Windows Services)
var logFile = Path.Combine(appDataFolder, "activity.log");
builder.Services.AddSingleton<ILoggerProvider>(sp => new FileLoggerProvider(logFile));

// Add console logging for debugging
builder.Services.AddLogging(configure =>
{
    configure.AddConsole();
    configure.AddDebug();
});

// ============================================================
// BUILD APPLICATION
// ============================================================

var app = builder.Build();

// ============================================================
// DATABASE INITIALIZATION
// ============================================================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider
            .GetRequiredService<TrackerDbContext>();
        db.Database.Migrate();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Database initialized at: {DbPath}", dbPath);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider
            .GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize database at: {DbPath}", dbPath);
        throw;
    }
}

// ============================================================
// START SERVICES
// ============================================================

var activityService = app.Services.GetRequiredService<ActivityService>();

// Determine if running as service
bool isService = WindowsServiceHelpers.IsWindowsService() && !isAgentMode;

if (!isService)
{
    // Only start hooks when running as console app
    try
    {
        var keyboard = app.Services.GetRequiredService<KeyboardHook>();
        keyboard.Start();

        var mouse = app.Services.GetRequiredService<MouseHook>();
        mouse.Start();

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Keyboard and mouse hooks started (Console mode)");
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to start keyboard/mouse hooks");
    }
}
else
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Running as Windows Service - Keyboard and mouse hooks disabled");
    logger.LogInformation("Using idle detection from GetLastInputInfo API");
}

// ============================================================
// RUN APPLICATION
// ============================================================

app.Run();