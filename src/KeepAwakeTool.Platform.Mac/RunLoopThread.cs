using System;
using System.Threading;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

/// <summary>
/// Owns a dedicated background thread running a CFRunLoop. Mirrors the Windows
/// hidden-message-pump design (one persistent thread; work marshaled onto it).
/// </summary>
internal sealed class RunLoopThread : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private IntPtr _runLoop;

    public RunLoopThread(string name, Action onRunLoopStarted)
    {
        _thread = new Thread(() =>
        {
            _runLoop = CoreFoundation.CFRunLoopGetCurrent();
            onRunLoopStarted();
            _ready.Set();
            CoreFoundation.CFRunLoopRun();
        }) { IsBackground = true, Name = name };
    }

    public IntPtr RunLoop => _runLoop;
    public void Start() { _thread.Start(); _ready.Wait(); }

    public void Dispose()
    {
        if (_runLoop != IntPtr.Zero) CoreFoundation.CFRunLoopStop(_runLoop);
        if (_thread.IsAlive) _thread.Join(2000);
        _ready.Dispose();
    }
}
