using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Mac;

/// <summary>Re-evaluates Accessibility trust on every access (no caching) so a
/// runtime grant is picked up on the next pump tick without a restart.</summary>
public sealed class MacPermissionGate : IPermissionGate
{
    public bool CanInjectInput => MacAccessibility.IsTrusted();
}
