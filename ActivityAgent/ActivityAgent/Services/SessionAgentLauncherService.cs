using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ActivityTracker.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ActivityTracker.Services;

public sealed class SessionAgentLauncherService : BackgroundService
{
    private readonly ILogger<SessionAgentLauncherService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private Process? _agentProcess;

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    public SessionAgentLauncherService(ILogger<SessionAgentLauncherService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session Agent Launcher Service Started.");

        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                uint activeSessionId = WTSGetActiveConsoleSessionId();
                if (activeSessionId == 0xFFFFFFFF)
                {
                    _logger.LogDebug("No active console session detected.");
                }
                else
                {
                    // Check if agent is already running in the active session
                    bool isRunning = false;
                    var currentProcesses = Process.GetProcessesByName("ActivityAgent");
                    foreach (var process in currentProcesses)
                    {
                        try
                        {
                            if (process.SessionId == (int)activeSessionId)
                            {
                                isRunning = true;
                                if (process.HasExited)
                                {
                                    isRunning = false;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore process access errors for other users' processes
                        }
                    }

                    if (!isRunning)
                    {
                        _logger.LogInformation("Interactive agent not running in active console session {SessionId}. Launching now...", activeSessionId);
                        
                        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath) || exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            exePath = Path.Combine(AppContext.BaseDirectory, "ActivityAgent.exe");
                        }

                        _logger.LogInformation("Spawning agent from path: {ExePath}", exePath);
                        _agentProcess = SessionAgentLauncher.LaunchAgentInUserSession(exePath, "--agent", _logger);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Session Agent Launcher Service loop.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Session Agent Launcher Service is stopping...");
        
        try
        {
            if (_agentProcess != null && !_agentProcess.HasExited)
            {
                _logger.LogInformation("Terminating spawned agent process PID {Pid} during service stop.", _agentProcess.Id);
                _agentProcess.Kill();
                await _agentProcess.WaitForExitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating spawned agent process during shutdown.");
        }

        await base.StopAsync(cancellationToken);
    }
}
