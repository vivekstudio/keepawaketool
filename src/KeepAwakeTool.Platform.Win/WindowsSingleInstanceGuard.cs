using System.Threading;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsSingleInstanceGuard : ISingleInstanceGuard
{
    private const string MutexName = @"Global\KeepAwakeTool";
    private Mutex? _mutex;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);
        return createdNew;
    }

    public void SignalExistingInstance()
    {
        // v1: rely on tray icon being present; named-pipe focus message can be added later.
    }

    public void Release()
    {
        try { _mutex?.ReleaseMutex(); } catch { /* may have been abandoned */ }
        _mutex?.Dispose();
        _mutex = null;
    }
}
