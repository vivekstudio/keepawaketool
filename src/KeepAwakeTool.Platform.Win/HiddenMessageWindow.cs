using System;
using System.Runtime.InteropServices;
using System.Threading;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

internal sealed class HiddenMessageWindow : IDisposable
{
    private const string ClassName = "KeepAwakeTool.HiddenMessageWindow";
    private const uint WM_APP_REGISTER   = 0x8000 + 1; // WM_APP+1
    private const uint WM_APP_UNREGISTER = 0x8000 + 2; // WM_APP+2

    private readonly WndProcDelegate _wndProc;
    private readonly Thread _pumpThread;
    private readonly Action<string, string>? _log;
    private readonly Action<bool>? _onRegisterResult;
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

    public HiddenMessageWindow(Action<string, string>? log = null, Action<bool>? onRegisterResult = null)
    {
        _log = log;
        _onRegisterResult = onRegisterResult;
        _wndProc = WndProc;
        _pumpThread = new Thread(Pump) { IsBackground = true, Name = "KAT-HotkeyPump" };
        _pumpThread.Start();
        _ready.Wait();
    }

    public IntPtr Handle => _hwnd;

    /// <summary>
    /// Request RegisterHotKey on the pump thread (safe to call from any thread).
    /// </summary>
    public void RequestRegister(int id, uint mods, uint vk)
        => User32.PostMessageW(_hwnd, WM_APP_REGISTER, (nint)id, (nint)((vk << 16) | mods));

    /// <summary>
    /// Request UnregisterHotKey on the pump thread (safe to call from any thread).
    /// </summary>
    public void RequestUnregister(int id)
        => User32.PostMessageW(_hwnd, WM_APP_UNREGISTER, (nint)id, 0);

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
            {
                HotkeyPressed?.Invoke((int)msg.wParam);
            }
            else if (msg.message == WM_APP_REGISTER)
            {
                int id = (int)msg.wParam;
                uint packed = (uint)(long)msg.lParam;
                uint mods = packed & 0xFFFF;
                uint vk = (packed >> 16) & 0xFFFF;
                bool ok = User32.RegisterHotKey(_hwnd, id, mods, vk);
                _log?.Invoke(ok ? "INFO" : "ERROR",
                    ok ? $"RegisterHotKey ok id={id} mods=0x{mods:X} vk=0x{vk:X}"
                       : $"RegisterHotKey FAILED id={id} mods=0x{mods:X} vk=0x{vk:X} err={Marshal.GetLastWin32Error()}");
                try { _onRegisterResult?.Invoke(ok); } catch { /* best effort */ }
            }
            else if (msg.message == WM_APP_UNREGISTER)
            {
                User32.UnregisterHotKey(_hwnd, (int)msg.wParam);
                _log?.Invoke("INFO", $"UnregisterHotKey id={(int)msg.wParam}");
            }

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
