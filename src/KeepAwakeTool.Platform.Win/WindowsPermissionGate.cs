using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

/// <summary>Windows has no input-permission concept; injection is always permitted.</summary>
public sealed class WindowsPermissionGate : IPermissionGate
{
    public bool CanInjectInput => true;
}
