using System.Threading;

namespace KeepAwakeTool.App.SingleInstance;

internal static class SingleInstanceGuard
{
    private const string MutexName = @"Global\KeepAwakeTool";
    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);
        return createdNew;
    }

    public static void SignalExistingInstance()
    {
        // v1: rely on tray icon being present; named-pipe focus message can be added later.
    }

    public static void Release()
    {
        try { _mutex?.ReleaseMutex(); } catch { /* may have been abandoned */ }
        _mutex?.Dispose();
        _mutex = null;
    }
}
