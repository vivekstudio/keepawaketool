using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsInputPermissionPrompt : IInputPermissionPrompt
{
    public void RequestInitialGrantIfNeeded() { /* no input-permission concept on Windows */ }
}
