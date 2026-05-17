using System;
using System.Runtime.InteropServices;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsIdleMonitor : IIdleMonitor
{
    public TimeSpan TimeSinceLastUserInput()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!User32.GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var tick = Kernel32.GetTickCount();
        var elapsedMs = unchecked(tick - info.dwTime);
        return TimeSpan.FromMilliseconds(elapsedMs);
    }
}
