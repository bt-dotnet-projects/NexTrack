using ActivityTracker.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ActivityTracker.Services;

public sealed class WindowTrackerService : BackgroundService
{
    private readonly ActivityService _activityService;
    private readonly ILogger<WindowTrackerService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(1);

    private DateTime _lastSaveTime = DateTime.UtcNow;
    private int _trackCount = 0;

    public WindowTrackerService(
        ActivityService activityService,
        ILogger<WindowTrackerService> logger)
    {
        _activityService = activityService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Window Tracker Service Started");

        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _trackCount++;

                try
                {
                    await TrackCurrentWindowAsync();

                    // Force save periodically
                    if (DateTime.UtcNow - _lastSaveTime >= SaveInterval)
                    {
                        await ForceSaveAsync();
                        _lastSaveTime = DateTime.UtcNow;
                    }

                    // Log every 100th track for debugging
                    if (_trackCount % 100 == 0)
                    {
                        _logger.LogDebug("Window tracker processed {Count} checks", _trackCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in window tracking loop (Check #{Count})", _trackCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Window Tracker Service is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Window Tracker Service");
        }
        finally
        {
            // Save the last active window during shutdown
            _logger.LogInformation("Saving final activity during shutdown...");
            await ForceSaveAsync();
            _logger.LogInformation("Window Tracker Service stopped. Total checks: {Count}", _trackCount);
        }
    }

    private async Task TrackCurrentWindowAsync()
    {
        try
        {
            // Skip tracking if currently idle
            if (_activityService.IsCurrentlyIdle())
            {
                _logger.LogDebug("Skipping window tracking while idle");
                return;
            }

            int pid = WindowHelper.GetActiveProcessId();

            if (pid <= 0)
            {
                _logger.LogDebug("No active process found (PID: {Pid})", pid);
                return;
            }

            string title = WindowHelper.GetActiveWindowTitle() ?? "No Title";
            string processName = ProcessHelper.GetProcessName(pid);
            string application = ProcessHelper.GetApplicationName(pid);

            // Validate we got valid data
            if (string.IsNullOrWhiteSpace(processName))
            {
                _logger.LogDebug("Invalid process name for PID: {Pid}", pid);
                return;
            }

            if (string.IsNullOrWhiteSpace(application))
            {
                application = processName; // Fallback to process name
            }

            // Log detailed tracking for debugging (every 10th track)
            if (_trackCount % 10 == 0)
            {
                _logger.LogDebug(
                    "Tracking window: PID={Pid}, Process={Process}, App={App}, Title={Title}",
                    pid, processName, application, title);
            }

            await _activityService.UpdateCurrentWindowAsync(
                processName,
                title,
                application);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking active window");
        }
    }

    private async Task ForceSaveAsync()
    {
        try
        {
            await _activityService.ForceSaveAsync();
            _logger.LogDebug("Activity force saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving current activity");
        }
    }
}