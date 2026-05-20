using System;
using System.IO;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacSingleInstanceGuard : ISingleInstanceGuard
{
    private FileStream? _lock;

    private static string LockPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeepAwakeTool");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "instance.lock");
    }

    public bool TryAcquire()
    {
        try
        {
            // FileShare.None gives an exclusive OS-level lock; a second process
            // opening the same path fails until this stream is closed/process dies.
            _lock = new FileStream(LockPath(), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void SignalExistingInstance()
    {
        // v1 parity: the running tray icon is the "already running" signal.
    }

    public void Release()
    {
        _lock?.Dispose();
        _lock = null;
    }
}
