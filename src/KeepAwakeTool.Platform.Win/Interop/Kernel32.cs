using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Win.Interop;

internal static class Kernel32
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();
}
