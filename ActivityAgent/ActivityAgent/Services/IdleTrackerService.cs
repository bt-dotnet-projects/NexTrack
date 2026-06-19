using ActivityAgent.Helpers;
using ActivityTracker.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ActivityTracker.Services;

public sealed class IdleTrackerService : BackgroundService
{
    private readonly ActivityService _activityService;
    private readonly ILogger<IdleTrackerService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    // Log a heartbeat every N checks even while idle/quiet, so a long gap with
    // zero log lines is always either "expected idle, heartbeat confirms alive"
    // or "process actually died" -- never ambiguous.
    private const int HeartbeatEveryNChecks = 30; // ~60s at a 2s poll interval

    private bool _wasIdle = false;
    private int _idleCheckCount = 0;

    public IdleTrackerService(
        ActivityService activityService,
        ILogger<IdleTrackerService> logger)
    {
        _activityService = activityService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Idle Tracker Service Started");

        bool idleAvailable = IdleHelper.IsIdleDetectionAvailable(_logger);
        _logger.LogInformation("Idle detection available: {Available}", idleAvailable);

        if (!idleAvailable)
        {
            _logger.LogWarning("Idle detection is not available - using fallback method");
        }

        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _idleCheckCount++;

                try
                {
                    bool isIdle = IdleHelper.IsIdle(_logger);

                    if (_idleCheckCount % HeartbeatEveryNChecks == 0)
                    {
                        int idleSeconds = IdleHelper.GetIdleSeconds(_logger);
                        _logger.LogInformation(
                            "Idle Tracker heartbeat #{Count}: IsIdle={IsIdle}, IdleSeconds={Seconds}",
                            _idleCheckCount, isIdle, idleSeconds);
                    }

                    if (!_wasIdle && isIdle)
                    {
                        _logger.LogInformation("User became idle (threshold reached)");
                        await _activityService.OnIdleStartedAsync();
                    }
                    else if (_wasIdle && !isIdle)
                    {
                        _logger.LogInformation("User became active (idle ended)");
                        await _activityService.OnIdleEndedAsync();
                    }

                    _wasIdle = isIdle;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during idle check #{Count}", _idleCheckCount);

                    if (_wasIdle)
                    {
                        _logger.LogWarning("Forcing idle state reset due to error");
                        await _activityService.ForceSaveIdleAsync();
                        _wasIdle = false;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Idle Tracker Service is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Idle Tracker Service");
        }
        finally
        {
            if (_wasIdle)
            {
                _logger.LogInformation("Cleaning up idle state during shutdown");
                await _activityService.ForceSaveIdleAsync();
            }

            _logger.LogInformation("Idle Tracker Service stopped. Total checks: {Count}", _idleCheckCount);

            // Honest notification -- reports the real outcome, never a fabricated error.
            ServiceLifecycleNotifier.NotifyStopped(_logger, "Activity Monitor");
        }
    }
}