using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ActivityAgent.Helpers;

/// <summary>
/// Sends an honest, truthful notification to the active console session when
/// the service is stopped. Uses the built-in Windows "msg.exe" utility, which
/// requires no extra UI process and works from a Session-0 service.
///
/// This intentionally does NOT fabricate error messages (e.g. fake "Access
/// Denied"). It reports what actually happened, which is the only acceptable
/// approach -- deceiving the user about whether their action succeeded is
/// not something this should ever do.
/// </summary>
public static class ServiceLifecycleNotifier
{
    public static void NotifyStopped(ILogger logger, string serviceName)
    {
        TryNotify(logger, $"{serviceName} has been stopped.");
    }

    public static void NotifyRestarted(ILogger logger, string serviceName)
    {
        TryNotify(logger, $"{serviceName} was stopped and has automatically restarted.");
    }

    private static void TryNotify(ILogger logger, string message)
    {
        try
        {
            // msg.exe sends a real message box to all sessions on the console.
            // /TIME:0 keeps it up until dismissed; remove for auto-timeout.
            var psi = new ProcessStartInfo
            {
                FileName = "msg.exe",
                Arguments = $"* /TIME:10 \"{message}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);

            logger.LogInformation("Sent session notification: {Message}", message);
        }
        catch (Exception ex)
        {
            // Notification failing should never crash the service or block shutdown.
            logger.LogWarning(ex, "Failed to send session notification (non-fatal)");
        }
    }
}