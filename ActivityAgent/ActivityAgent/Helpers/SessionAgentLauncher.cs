using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ActivityTracker.Helpers;

public static class SessionAgentLauncher
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        int ImpersonationLevel,
        int TokenType,
        out IntPtr phNewToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUserW(
        IntPtr hToken,
        string? lpApplicationName,
        string? lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint TOKEN_ALL_ACCESS = 0xF01FF;
    
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    public static Process? LaunchAgentInUserSession(string exePath, string arguments, ILogger logger)
    {
        uint sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
        {
            logger.LogWarning("No active console session found to launch the agent.");
            return null;
        }

        logger.LogInformation("Active console session ID: {SessionId}", sessionId);

        if (!WTSQueryUserToken(sessionId, out IntPtr userToken))
        {
            int error = Marshal.GetLastWin32Error();
            logger.LogError("WTSQueryUserToken failed for session {SessionId}. Win32 Error: {Error}. Service must run as LocalSystem.", sessionId, error);
            return null;
        }

        IntPtr duplicatedToken = IntPtr.Zero;
        IntPtr envBlock = IntPtr.Zero;
        try
        {
            if (!DuplicateTokenEx(
                userToken,
                TOKEN_ALL_ACCESS,
                IntPtr.Zero,
                SecurityImpersonation,
                TokenPrimary,
                out duplicatedToken))
            {
                int error = Marshal.GetLastWin32Error();
                logger.LogError("DuplicateTokenEx failed. Win32 Error: {Error}", error);
                return null;
            }

            if (!CreateEnvironmentBlock(out envBlock, duplicatedToken, false))
            {
                int error = Marshal.GetLastWin32Error();
                logger.LogWarning("CreateEnvironmentBlock failed. Win32 Error: {Error}. Spawning process without environment block.", error);
                envBlock = IntPtr.Zero;
            }

            STARTUPINFO si = new();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = @"WinSta0\Default";

            string commandLine = $"\"{exePath}\" {arguments}";

            string? workingDir = System.IO.Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(workingDir))
            {
                workingDir = AppContext.BaseDirectory;
            }

            logger.LogInformation("Launching agent: {CommandLine} with WorkingDir: {WorkingDir}", commandLine, workingDir);

            uint creationFlags = CREATE_UNICODE_ENVIRONMENT;

            if (!CreateProcessAsUserW(
                duplicatedToken,
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                creationFlags,
                envBlock,
                workingDir,
                ref si,
                out PROCESS_INFORMATION pi))
            {
                int error = Marshal.GetLastWin32Error();
                logger.LogError("CreateProcessAsUserW failed. Win32 Error: {Error}", error);
                return null;
            }

            logger.LogInformation("Successfully launched agent process. PID: {Pid}", pi.dwProcessId);

            try
            {
                var process = Process.GetProcessById(pi.dwProcessId);
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
                return process;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get Process object for PID: {Pid}", pi.dwProcessId);
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
                return null;
            }
        }
        finally
        {
            if (userToken != IntPtr.Zero) CloseHandle(userToken);
            if (duplicatedToken != IntPtr.Zero) CloseHandle(duplicatedToken);
            if (envBlock != IntPtr.Zero) DestroyEnvironmentBlock(envBlock);
        }
    }
}
