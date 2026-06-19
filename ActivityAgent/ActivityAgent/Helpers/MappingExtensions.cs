using ActivityTracker.DTOs;
using ActivityTracker.Models;

namespace ActivityTracker.Helpers;

public static class MappingExtensions
{
    public static ActivityDto ToDto(
        this ActivityLog log)
    {
        return new ActivityDto
        {
            MachineId = log.MachineId,
            UserName = log.UserName,
            ProcessName = log.ProcessName,
            WindowTitle = log.WindowTitle,
            Application = log.Application,
            StartTime = log.StartTime,
            EndTime = log.EndTime,
            DurationSeconds = log.DurationSeconds,
            IsIdle = log.IsIdle,
            KeyboardCount = log.KeyboardCount,
            MouseCount = log.MouseCount
        };
    }

    public static ActivityApiModel ToApiModel(this ActivityLog log)
    {
        return new ActivityApiModel
        {
            Id = Guid.NewGuid(),
            EmployeeId = log.UserName,
            MachineId = log.MachineId,
            AppName = log.Application,
            WindowTitle = log.WindowTitle,
            StartTimeUtc = log.StartTime.ToUniversalTime(),
            EndTimeUtc = log.EndTime.ToUniversalTime(),
            DurationSeconds = log.DurationSeconds
        };
    }

    public static ActivitySyncRequest ToSyncRequest(this IEnumerable<ActivityLog> logs)
    {
        var req = new ActivitySyncRequest();

        req.Activities.AddRange(logs.Select(l => l.ToApiModel()));

        return req;
    }
}