namespace KeepAwakeTool.Core.Platform;

/// <summary>
/// One-time, app-startup request to surface the OS input-permission prompt so the app
/// is listed in the relevant settings pane. Windows: no-op. macOS: AXIsProcessTrusted
/// with the prompt option, only when not already trusted.
/// </summary>
public interface IInputPermissionPrompt
{
    void RequestInitialGrantIfNeeded();
}
