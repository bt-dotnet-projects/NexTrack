using System.Diagnostics;

namespace ActivityTracker.Helpers;

public static class ProcessHelper
{
    public static string GetProcessName(int processId)
    {
        try
        {
            if (processId <= 0)
                return string.Empty;


            using Process process =
                Process.GetProcessById(processId);


            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }


    public static string GetApplicationName(
        int processId)
    {
        try
        {
            if (processId <= 0)
                return "Unknown";


            using Process process =
                Process.GetProcessById(processId);


            return process.MainModule?.FileVersionInfo
                .ProductName
                ?? process.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }


    public static string GetExecutablePath(
        int processId)
    {
        try
        {
            using Process process =
                Process.GetProcessById(processId);


            return process.MainModule?.FileName
                ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}