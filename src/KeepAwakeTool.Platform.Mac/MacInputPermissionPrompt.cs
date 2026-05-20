using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacInputPermissionPrompt : IInputPermissionPrompt
{
    public void RequestInitialGrantIfNeeded()
    {
        // Only prompt when not already trusted; RequestTrust shows the system
        // dialog and registers the app in Privacy ▸ Accessibility (toggle off).
        if (!MacAccessibility.IsTrusted())
            MacAccessibility.RequestTrust();
    }
}
