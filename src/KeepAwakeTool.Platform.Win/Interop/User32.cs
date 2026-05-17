using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Win.Interop;

internal static class User32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint SendMessageW(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint SendMessageTimeoutW(nint hWnd, uint Msg, nint wParam, nint lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessageW(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    public const uint WM_SYSCOMMAND  = 0x0112;
    public const int  SC_MONITORPOWER = 0xF170;
    public static readonly nint HWND_BROADCAST = new(0xFFFF);
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    public const uint MOD_ALT     = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT   = 0x0004;
    public const uint MOD_WIN     = 0x0008;
    public const uint WM_HOTKEY   = 0x0312;
}
