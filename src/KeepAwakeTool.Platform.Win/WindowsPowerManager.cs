using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsPowerManager : IPowerManager
{
    public void KeepSystemAwake(bool on)
    {
        var flags = on
            ? ExecutionState.Continuous | ExecutionState.SystemRequired
            : ExecutionState.Continuous;
        Kernel32.SetThreadExecutionState(flags);
    }

    public void ForceDisplayOff()
    {
        // SC_MONITORPOWER lParam = 2 (display off). WM_SYSCOMMAND must be SENT,
        // not posted (a broadcast PostMessage of WM_SYSCOMMAND is dropped).
        // SendMessageTimeout with ABORTIFHUNG + 1s timeout prevents the caller
        // (engine pump thread) from hanging on an unresponsive top-level window.
        User32.SendMessageTimeoutW(
            User32.HWND_BROADCAST, User32.WM_SYSCOMMAND, (nint)User32.SC_MONITORPOWER, 2,
            User32.SMTO_ABORTIFHUNG, 1000, out _);
    }
}
