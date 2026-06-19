using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace ActivityTracker.Helpers;

/// <summary>
/// Detects user idle time. When running as a Windows Service (Session 0),
/// GetLastInputInfo only sees Session 0 input (none), so we query the
/// active console session's WTSINFO via WTS APIs instead.
///
/// WTSINFO struct (x64 and x86 with default packing):
///   Offset   0: State (4), SessionId (4), 6×DWORD counters (24)  = 32 bytes
///   Offset  32: WinStationName  WCHAR[32]                        = 64 bytes
///   Offset  96: Domain          WCHAR[17]                        = 34 bytes
///   Offset 130: UserName        WCHAR[21]                        = 42 bytes
///   Offset 172: (4 bytes padding to 8-byte boundary)
///   Offset 176: ConnectTime     LARGE_INTEGER                    =  8 bytes
///   Offset 184: DisconnectTime  LARGE_INTEGER                    =  8 bytes
///   Offset 192: LastInputTime   LARGE_INTEGER                    =  8 bytes  ← we need this
///   Offset 200: LogonTime       LARGE_INTEGER                    =  8 bytes
///   Offset 208: CurrentTime     LARGE_INTEGER                    =  8 bytes  ← and this
///   Total : 216 bytes
/// </summary>
public static class IdleHelper
{
    private const int IdleThresholdSeconds = 30;

    // ── Expected WTSINFO byte offsets ──
    private const int WTSINFO_EXPECTED_SIZE = 216;
    private const int OFFSET_SESSION_ID = 4;
    private const int OFFSET_LAST_INPUT = 192;
    private const int OFFSET_CURRENT_TIME = 208;

    // ── One-time diagnostic flag ──
    private static bool _loggedDiagnostics = false;

    // ── Win32: GetLastInputInfo (works in interactive sessions only) ──

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    // ── Win32: WTS APIs (works cross-session, needed for Session 0) ──

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQuerySessionInformationW(
        IntPtr hServer,
        uint sessionId,
        int wtsInfoClass,
        out IntPtr ppBuffer,
        out uint pBytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;
    private const int WTSSessionInfo = 24; // WTS_INFO_CLASS value for WTSINFO

    // ── Public API ──

    public static int GetIdleSeconds(ILogger? logger = null)
    {
        try
        {
            // First, try the cross-session WTS method (works from Session 0)
            int? wtsIdle = GetIdleSecondsViaWts(logger);
            if (wtsIdle.HasValue)
                return wtsIdle.Value;

            // Fallback: use GetLastInputInfo (works only in interactive session)
            // When running as a Windows Service (Session 0), GetLastInputInfo queries the session 0 status
            // where there is no user input. This will always return a false positive idle state.
            if (Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService())
            {
                logger?.LogDebug("WTS idle query returned null while running as a Windows Service. Returning 0 to prevent false idle detection.");
                return 0;
            }

            return GetIdleSecondsViaLastInput(logger);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "GetIdleSeconds threw unexpectedly");
            return 0;
        }
    }

    public static bool IsIdle(ILogger? logger = null)
    {
        return GetIdleSeconds(logger) >= IdleThresholdSeconds;
    }

    public static bool IsIdleDetectionAvailable(ILogger? logger = null)
    {
        try
        {
            // Check WTS method first
            uint sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId != 0xFFFFFFFF)
            {
                logger?.LogInformation(
                    "WTS idle detection available. Active console session: {SessionId}",
                    sessionId);
                return true;
            }

            // Fallback: check GetLastInputInfo
            LASTINPUTINFO info = new()
            {
                cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
            };

            bool ok = GetLastInputInfo(ref info);

            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                logger?.LogWarning(
                    "IsIdleDetectionAvailable check failed with Win32 error {Error}",
                    err);
            }

            return ok;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "IsIdleDetectionAvailable threw unexpectedly");
            return false;
        }
    }

    // ── Private helpers ──

    /// <summary>
    /// Query the active console session's last input time via WTS.
    /// Reads raw buffer bytes at known offsets to avoid struct marshaling issues.
    /// Returns null if the WTS query fails (caller should fall back).
    /// </summary>
    private static int? GetIdleSecondsViaWts(ILogger? logger)
    {
        uint sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
        {
            logger?.LogDebug("No active console session found");
            return null;
        }

        if (!WTSQuerySessionInformationW(
                WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                WTSSessionInfo,
                out IntPtr buffer,
                out uint bytesReturned))
        {
            int err = Marshal.GetLastWin32Error();
            logger?.LogWarning(
                "WTSQuerySessionInformation failed for session {SessionId} with error {Error}",
                sessionId, err);
            return null;
        }

        try
        {
            // Verify we received enough data
            if (bytesReturned < WTSINFO_EXPECTED_SIZE)
            {
                logger?.LogWarning(
                    "WTS buffer too small: got {Bytes} bytes, expected {Expected}. " +
                    "Falling back to GetLastInputInfo.",
                    bytesReturned, WTSINFO_EXPECTED_SIZE);
                return null;
            }

            // Read SessionId from buffer to verify our offsets are correct
            int bufferSessionId = Marshal.ReadInt32(buffer, OFFSET_SESSION_ID);

            // Read the two FILETIME values we need (100-nanosecond intervals since 1601-01-01)
            long lastInputTime = Marshal.ReadInt64(buffer, OFFSET_LAST_INPUT);
            long currentTime = Marshal.ReadInt64(buffer, OFFSET_CURRENT_TIME);

            // One-time diagnostic log on first successful read
            if (!_loggedDiagnostics)
            {
                _loggedDiagnostics = true;
                logger?.LogInformation(
                    "WTS idle diagnostics: SessionId(queried)={QueriedId}, " +
                    "SessionId(buffer)={BufferId}, BytesReturned={Bytes}, " +
                    "LastInputTime={LastInput}, CurrentTime={Current}, " +
                    "LastInputUTC={LastInputUTC}, CurrentUTC={CurrentUTC}",
                    sessionId, bufferSessionId, bytesReturned,
                    lastInputTime, currentTime,
                    lastInputTime > 0 ? DateTime.FromFileTimeUtc(lastInputTime).ToString("o") : "N/A",
                    currentTime > 0 ? DateTime.FromFileTimeUtc(currentTime).ToString("o") : "N/A");
            }

            // Sanity check: verify SessionId matches
            if (bufferSessionId != (int)sessionId)
            {
                logger?.LogWarning(
                    "WTS SessionId mismatch: queried {Queried} but buffer has {Buffer}. " +
                    "Struct layout may be wrong — falling back.",
                    sessionId, bufferSessionId);
                return null;
            }

            if (lastInputTime <= 0 || currentTime <= 0)
            {
                logger?.LogDebug(
                    "WTS returned zero/negative timestamps — session may not be fully initialized");
                return null;
            }

            long idleFiletimeTicks = currentTime - lastInputTime;
            int idleSeconds = (int)(idleFiletimeTicks / 10_000_000); // FILETIME ticks → seconds

            // Sanity: if negative (clock skew) or unreasonably large (> 30 days), treat as suspect
            if (idleSeconds < 0)
            {
                logger?.LogWarning(
                    "WTS idle seconds negative ({Seconds}), treating as 0", idleSeconds);
                return 0;
            }

            if (idleSeconds > 30 * 24 * 3600)
            {
                logger?.LogWarning(
                    "WTS idle seconds unreasonably large ({Seconds}), falling back",
                    idleSeconds);
                return null;
            }

            return idleSeconds;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    /// <summary>
    /// Classic GetLastInputInfo — only works within the same interactive session.
    /// </summary>
    private static int GetIdleSecondsViaLastInput(ILogger? logger)
    {
        LASTINPUTINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref info))
        {
            int err = Marshal.GetLastWin32Error();
            logger?.LogWarning(
                "GetLastInputInfo failed with Win32 error {Error}. " +
                "This can happen on a locked session / disconnected RDP.",
                err);
            return 0;
        }

        long systemUptimeMs = Environment.TickCount64;

        uint dwTime = info.dwTime;
        uint lowUptime = unchecked((uint)systemUptimeMs);

        uint idleTicks = lowUptime >= dwTime
            ? lowUptime - dwTime
            : (uint.MaxValue - dwTime) + lowUptime + 1;

        return (int)(idleTicks / 1000);
    }
}