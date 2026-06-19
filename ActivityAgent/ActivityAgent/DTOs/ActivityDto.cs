namespace ActivityTracker.DTOs;

public class ActivityDto
{
    public string MachineId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string ProcessName { get; set; } = string.Empty;

    public string WindowTitle { get; set; } = string.Empty;

    public string Application { get; set; } = string.Empty;


    public DateTime StartTime { get; set; }


    public DateTime EndTime { get; set; }


    public int DurationSeconds { get; set; }


    public bool IsIdle { get; set; }


    public int KeyboardCount { get; set; }


    public int MouseCount { get; set; }
}