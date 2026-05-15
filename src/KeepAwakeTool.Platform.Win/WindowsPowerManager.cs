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
        // SC_MONITORPOWER lParam = 2 (off). PostMessage so we don't block.
        User32.PostMessageW(User32.HWND_BROADCAST, User32.WM_SYSCOMMAND, (nint)User32.SC_MONITORPOWER, 2);
    }
}
