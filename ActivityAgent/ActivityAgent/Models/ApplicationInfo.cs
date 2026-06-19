namespace ActivityTracker.Models;

public class ApplicationInfo
{
    public string ProcessName { get; set; } = string.Empty;

    public string WindowTitle { get; set; } = string.Empty;

    public string ApplicationName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }
}