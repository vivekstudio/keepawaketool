using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Mac;

/// <summary>Screen-sharing/remote detection is explicitly out of scope (spec §2).
/// The sole consumer is the RDP tooltip note; returning false is correct v2 behavior.</summary>
public sealed class MacSessionInfo : ISessionInfo
{
    public bool IsRemoteSession => false;
}
