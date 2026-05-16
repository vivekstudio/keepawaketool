using System;
using System.Runtime.InteropServices;
using System.Threading;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

internal sealed class HiddenMessageWindow : IDisposable
{
    private const string ClassName = "KeepAwakeTool.HiddenMessageWindow";
    private readonly WndProcDelegate _wndProc;
    private readonly Thread _pumpThread;
    private IntPtr _hwnd;
    private bool _disposed;

    public event Action<int>? HotkeyPressed;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASS { public uint style; public WndProcDelegate lpfnWndProc; public int cbClsExtra; public int cbWndExtra; public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground; [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName; [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName; }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowExW(uint dwExStyle, [MarshalAs(UnmanagedType.LPWStr)] string lpClassName, [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName, uint dwStyle, int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX; public int ptY; }

    private const uint WM_QUIT = 0x0012;
    private const uint HWND_MESSAGE = unchecked((uint)-3);
    private uint _pumpThreadId;
    private readonly ManualResetEventSlim _ready = new(false);

    public HiddenMessageWindow()
    {
        _wndProc = WndProc;
        _pumpThread = new Thread(Pump) { IsBackground = true, Name = "KAT-HotkeyPump" };
        _pumpThread.Start();
        _ready.Wait();
    }

    public IntPtr Handle => _hwnd;

    private void Pump()
    {
        _pumpThreadId = GetCurrentThreadId();
        var hInstance = GetModuleHandleW(null);
        var wc = new WNDCLASS { lpfnWndProc = _wndProc, hInstance = hInstance, lpszClassName = ClassName };
        RegisterClassW(ref wc);
        _hwnd = CreateWindowExW(0, ClassName, "", 0, 0, 0, 0, 0, (IntPtr)unchecked((int)HWND_MESSAGE), IntPtr.Zero, hInstance, IntPtr.Zero);
        _ready.Set();

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            // WM_HOTKEY is a thread message (msg.hwnd == NULL): DispatchMessage
            // will NOT route it to WndProc, so handle it directly here.
            if (msg.message == User32.WM_HOTKEY)
                HotkeyPressed?.Invoke((int)msg.wParam);

            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        => DefWindowProcW(hWnd, msg, wParam, lParam);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PostThreadMessageW(_pumpThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _pumpThread.Join(2000);
    }
}
