using System.ComponentModel.DataAnnotations;

namespace ActivityTracker.Models;

public class ActivityLog
{
    [Key]
    public long Id { get; set; }


    [Required]
    [MaxLength(100)]
    public string MachineId { get; set; } = string.Empty;


    [Required]
    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;


    [MaxLength(200)]
    public string ProcessName { get; set; } = string.Empty;


    [MaxLength(500)]
    public string WindowTitle { get; set; } = string.Empty;


    [MaxLength(200)]
    public string Application { get; set; } = string.Empty;


    public DateTime StartTime { get; set; }


    public DateTime EndTime { get; set; }


    public int DurationSeconds { get; set; }


    public bool IsIdle { get; set; }


    public int KeyboardCount { get; set; }


    public int MouseCount { get; set; }


    public bool IsSynced { get; set; }


    public DateTime CreatedAt { get; set; }
}