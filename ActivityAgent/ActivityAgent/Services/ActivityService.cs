using ActivityAgent.Configuration;
using ActivityTracker.Data;
using ActivityTracker.Helpers;
using ActivityTracker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActivityTracker.Services;

public sealed class ActivityService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KeyboardHook _keyboard;
    private readonly MouseHook _mouse;
    private readonly ILogger<ActivityService> _logger;
    private readonly AppConfig _appConfig;

    private readonly object _syncLock = new();
    private readonly string _machineId;
    private readonly string _userName;

    private ApplicationInfo? _currentApp;
    private bool _isCurrentlyIdle;
    private DateTime _lastActivityTime = DateTime.UtcNow;

    public ActivityService(
        IServiceScopeFactory scopeFactory,
        KeyboardHook keyboard,
        MouseHook mouse,
        ILogger<ActivityService> logger,
        IOptions<AppConfig> appConfigOptions)
    {
        _scopeFactory = scopeFactory;
        _keyboard = keyboard;
        _mouse = mouse;
        _logger = logger;
        _appConfig = appConfigOptions.Value;

        // Get MachineId and UserName from appconfig.json with fallbacks
        _machineId = !string.IsNullOrEmpty(_appConfig.MachineId)
            ? _appConfig.MachineId
            : Environment.MachineName;

        _userName = !string.IsNullOrEmpty(_appConfig.EmployeeId)
            ? _appConfig.EmployeeId
            : Environment.UserName;

        _isCurrentlyIdle = false;

        // Log the configuration being used
        _logger.LogInformation("ActivityService initialized with MachineId: {MachineId}, UserName: {UserName}",
            _machineId, _userName);
    }

    public async Task OnIdleStartedAsync()
    {
        ApplicationInfo? previousApp = null;

        lock (_syncLock)
        {
            if (_isCurrentlyIdle)
            {
                _logger.LogDebug("Already in idle state, ignoring idle start");
                return;
            }

            _logger.LogInformation("Idle state started");
            _isCurrentlyIdle = true;
            previousApp = _currentApp;

            _currentApp = new ApplicationInfo
            {
                ProcessName = "Idle",
                WindowTitle = "Idle",
                ApplicationName = "Idle",
                StartTime = DateTime.UtcNow
            };
        }

        if (previousApp != null)
        {
            // Save previous activity as non-idle
            await SaveActivityAsync(previousApp, isIdleOverride: false);
        }
    }

    public async Task OnIdleEndedAsync()
    {
        ApplicationInfo? idleApp = null;

        lock (_syncLock)
        {
            if (!_isCurrentlyIdle)
            {
                _logger.LogDebug("Not in idle state, ignoring idle end");
                return;
            }

            _logger.LogInformation("Idle state ended");
            _isCurrentlyIdle = false;
            idleApp = _currentApp;
            _currentApp = null;
            _lastActivityTime = DateTime.UtcNow;
        }

        // Save the idle activity
        if (idleApp != null)
        {
            await SaveActivityAsync(idleApp, isIdleOverride: true);
        }
    }

    public async Task UpdateCurrentWindowAsync(
        string processName,
        string windowTitle,
        string application)
    {
        // Skip if idle or if process/application is invalid
        if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(application))
        {
            _logger.LogDebug("Skipping update with invalid process or application");
            return;
        }

        lock (_syncLock)
        {
            if (_isCurrentlyIdle)
            {
                _logger.LogDebug("Skipping window update while idle");
                return;
            }

            // Same window, nothing to do
            if (_currentApp != null &&
                _currentApp.ProcessName == processName &&
                _currentApp.WindowTitle == windowTitle)
            {
                return;
            }

            // Update last activity time
            _lastActivityTime = DateTime.UtcNow;
        }

        ApplicationInfo? previousApp = null;

        lock (_syncLock)
        {
            // Store previous activity and start new one
            previousApp = _currentApp;

            _currentApp = new ApplicationInfo
            {
                ProcessName = processName,
                WindowTitle = windowTitle ?? "No Title",
                ApplicationName = application,
                StartTime = DateTime.UtcNow
            };
        }

        // Save old activity outside lock
        if (previousApp != null)
        {
            await SaveActivityAsync(previousApp);
        }
    }

    public async Task ForceSaveAsync()
    {
        ApplicationInfo? app;

        lock (_syncLock)
        {
            if (_isCurrentlyIdle)
            {
                _logger.LogDebug("Not saving while idle");
                return;
            }

            app = _currentApp;
            _currentApp = null;
        }

        if (app != null)
        {
            await SaveActivityAsync(app);
            _logger.LogDebug("Force saved activity for {Application}", app.ApplicationName);
        }
    }

    public async Task ForceSaveIdleAsync()
    {
        ApplicationInfo? idleApp;

        lock (_syncLock)
        {
            if (!_isCurrentlyIdle || _currentApp == null)
                return;

            idleApp = _currentApp;
            _currentApp = null;
            _isCurrentlyIdle = false;
        }

        if (idleApp != null)
        {
            await SaveActivityAsync(idleApp, isIdleOverride: true);
            _logger.LogDebug("Force saved idle activity");
        }
    }

    private async Task SaveActivityAsync(ApplicationInfo app, bool? isIdleOverride = null)
    {
        try
        {
            var endTime = DateTime.UtcNow;
            int duration = (int)(endTime - app.StartTime).TotalSeconds;

            // Ignore very short activities (less than 1 second)
            if (duration < 1)
            {
                _logger.LogDebug("Skipping activity with duration {Duration}s: {Application}",
                    duration, app.ApplicationName);
                return;
            }

            // Determine if this is idle
            bool isIdle;
            if (isIdleOverride.HasValue)
            {
                isIdle = isIdleOverride.Value;
            }
            else
            {
                // Check idle state based on user input
                isIdle = IdleHelper.IsIdle();
            }

            // Only get keyboard/mouse counts if not idle
            int keyboardCount = isIdle ? 0 : (int)_keyboard.ResetCount();
            int mouseCount = isIdle ? 0 : (int)_mouse.ResetCount();

            var activity = new ActivityLog
            {
                MachineId = _machineId,      // From appconfig.json with fallback
                UserName = _userName,        // From appconfig.json (EmployeeId) with fallback
                ProcessName = app.ProcessName ?? "Unknown",
                WindowTitle = app.WindowTitle ?? "Unknown",
                Application = app.ApplicationName ?? "Unknown",
                StartTime = app.StartTime,
                EndTime = endTime,
                DurationSeconds = duration,
                IsIdle = isIdle,
                KeyboardCount = keyboardCount,
                MouseCount = mouseCount,
                IsSynced = false,
                CreatedAt = DateTime.UtcNow
            };

            using (var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
                await repository.AddActivityAsync(activity);
            }

            _logger.LogInformation(
                "Activity saved: {Application}, Duration: {Duration}s, Idle: {IsIdle}, Keys: {Keys}, Mouse: {Mouse}, User: {User}, Machine: {Machine}",
                activity.Application,
                activity.DurationSeconds,
                activity.IsIdle,
                activity.KeyboardCount,
                activity.MouseCount,
                activity.UserName,
                activity.MachineId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save activity for {Application}",
                app.ApplicationName ?? "Unknown");
        }
    }

    public bool IsCurrentlyIdle()
    {
        lock (_syncLock)
        {
            return _isCurrentlyIdle;
        }
    }
}