namespace KeepAwakeTool.Core.Platform;

/// <summary>
/// Single-instance enforcement. Windows: named Mutex. macOS: exclusive lock file
/// (.NET named mutexes are process-local on macOS so cannot work cross-instance).
/// </summary>
public interface ISingleInstanceGuard
{
    /// <summary>True if this process acquired the single-instance token.</summary>
    bool TryAcquire();
    /// <summary>v1 no-op; the running tray icon is the "already running" signal.</summary>
    void SignalExistingInstance();
    void Release();
}
