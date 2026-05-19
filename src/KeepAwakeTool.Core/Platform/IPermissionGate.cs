namespace KeepAwakeTool.Core.Platform;

/// <summary>
/// Whether the OS currently permits synthetic input injection.
/// Windows has no analog (always true). macOS requires Accessibility trust.
/// Re-evaluated on every access so a runtime permission grant is picked up
/// on the next pump tick without an app restart.
/// </summary>
public interface IPermissionGate
{
    bool CanInjectInput { get; }
}

/// <summary>Default gate used when no platform gate is supplied (always permits).</summary>
public sealed class AlwaysAllowPermissionGate : IPermissionGate
{
    public bool CanInjectInput => true;
}
