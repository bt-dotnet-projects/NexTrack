namespace ActivityTracker.DTOs;

public class ActivityApiModel
{
    public Guid Id { get; set; }

    public string EmployeeId { get; set; } = string.Empty;

    public string MachineId { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public string WindowTitle { get; set; } = string.Empty;

    public DateTime StartTimeUtc { get; set; }

    public DateTime EndTimeUtc { get; set; }

    public int DurationSeconds { get; set; }
}

public class ActivitySyncRequest
{
    public List<ActivityApiModel> Activities { get; set; } = new();
}
