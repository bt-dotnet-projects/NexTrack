using System.Runtime.InteropServices;
using System.Text;

namespace ActivityTracker.Helpers;

public static class WindowHelper
{
    private const int MaxTitleLength = 1024;


    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();


    [DllImport("user32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        IntPtr hWnd,
        StringBuilder text,
        int count);


    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId);


    public static string GetActiveWindowTitle()
    {
        IntPtr handle = GetForegroundWindow();

        if (handle == IntPtr.Zero)
            return string.Empty;


        StringBuilder buffer =
            new(MaxTitleLength);


        GetWindowText(
            handle,
            buffer,
            buffer.Capacity);


        return buffer.ToString();
    }


    public static int GetActiveProcessId()
    {
        IntPtr handle = GetForegroundWindow();

        if (handle == IntPtr.Zero)
            return 0;


        GetWindowThreadProcessId(
            handle,
            out uint processId);


        return (int)processId;
    }
}