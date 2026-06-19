using System.Runtime.InteropServices;
using System.Threading;

namespace ActivityTracker.Helpers;

public class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;


    private IntPtr _hookId = IntPtr.Zero;
    private HookCallback? _callback;
    private long _keyCount;
    private System.Threading.Thread? _hookThread;
    private uint _hookThreadId;
    private System.Threading.AutoResetEvent? _startEvent;


    public long KeyCount =>
        Interlocked.Read(ref _keyCount);


    public void Start()
    {
        if (_hookId != IntPtr.Zero)
            return;

        _startEvent = new System.Threading.AutoResetEvent(false);
        _hookThread = new System.Threading.Thread(() =>
        {
            _hookThreadId = GetCurrentThreadId();
            _callback = HookProc;
            _hookId = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                _callback,
                GetModuleHandle(null),
                0);

            _startEvent.Set();

            if (_hookId != IntPtr.Zero)
            {
                MSG msg;
                while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
        });
        _hookThread.IsBackground = true;
        _hookThread.Start();
        _startEvent.WaitOne(2000);
    }


    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;

        if (_hookThreadId != 0)
        {
            const uint WM_QUIT = 0x0012;
            PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _hookThreadId = 0;
        }

        if (_hookThread != null)
        {
            _hookThread.Join(2000);
            _hookThread = null;
        }
    }


    public long ResetCount()
    {
        return Interlocked.Exchange(
            ref _keyCount,
            0);
    }


    private IntPtr HookProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = wParam.ToInt32();

            if (message == WM_KEYDOWN ||
                message == WM_SYSKEYDOWN)
            {
                Interlocked.Increment(
                    ref _keyCount);
            }
        }


        return CallNextHookEx(
            _hookId,
            nCode,
            wParam,
            lParam);
    }


    public void Dispose()
    {
        Stop();

        GC.SuppressFinalize(this);
    }


    private delegate IntPtr HookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);


    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        HookCallback callback,
        IntPtr hModule,
        uint threadId);


    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(
        IntPtr hook);


    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hook,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);


    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(
        string? moduleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(
        uint idThread,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(
        out MSG lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(
        ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(
        ref MSG lpMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }
}