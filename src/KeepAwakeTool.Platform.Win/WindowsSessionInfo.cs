using System.Runtime.InteropServices;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsSessionInfo : ISessionInfo
{
    private const int SM_REMOTESESSION = 0x1000;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public bool IsRemoteSession => GetSystemMetrics(SM_REMOTESESSION) != 0;
}
