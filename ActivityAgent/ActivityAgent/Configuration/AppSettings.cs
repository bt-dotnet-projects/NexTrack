namespace ActivityTracker.Configuration;


public class AppSettings
{
    public ApiSettings Api { get; set; } = new();
}


public class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;

    public int SyncMinutes { get; set; } = 2;

    public int BatchSize { get; set; } = 100;

    // Set to true for local development when server certificate
    // validation needs to be bypassed. Do NOT enable in production.
    public bool IgnoreSslErrors { get; set; } = false;
}