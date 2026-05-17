using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Win.Interop;

internal enum InputType : uint { Mouse = 0, Keyboard = 1, Hardware = 2 }

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public nint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public nint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint type;
    public InputUnion U;
}

internal static class MouseFlags
{
    public const uint Move     = 0x0001;
    public const uint Absolute = 0x8000;
}

internal static class KeyFlags
{
    public const uint KeyDown = 0x0000;
    public const uint KeyUp   = 0x0002;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LASTINPUTINFO
{
    public uint cbSize;
    public uint dwTime;
}
