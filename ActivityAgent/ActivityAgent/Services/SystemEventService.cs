using Microsoft.Win32;
using System.Runtime.Versioning;

namespace ActivityTracker.Services;

[SupportedOSPlatform("windows")]
public class SystemEventService : IDisposable
{
    private readonly ActivityService _activityService;


    public SystemEventService(
        ActivityService activityService)
    {
        _activityService = activityService;

        SystemEvents.SessionSwitch += OnSessionSwitch;

        SystemEvents.SessionEnding += OnSessionEnding;
    }


    private void OnSessionSwitch(
        object? sender,
        SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:

                _ = _activityService
                    .ForceSaveAsync();

                break;


            case SessionSwitchReason.SessionLogoff:

                _ = _activityService
                    .ForceSaveAsync();

                break;
        }
    }


    private void OnSessionEnding(
        object? sender,
        SessionEndingEventArgs e)
    {
        _ = _activityService
            .ForceSaveAsync();
    }


    public void Dispose()
    {
        SystemEvents.SessionSwitch -=
            OnSessionSwitch;

        SystemEvents.SessionEnding -=
            OnSessionEnding;
    }
}