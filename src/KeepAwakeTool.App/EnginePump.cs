using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Diagnostics;
using KeepAwakeTool.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App;

public sealed class EnginePump : IDisposable
{
    private readonly IServiceProvider _sp;
    private readonly Scheduler _scheduler;
    private readonly FileLogger _logger;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;

    public EnginePump(IServiceProvider sp)
    {
        _sp = sp;
        _scheduler = sp.GetRequiredService<Scheduler>();
        _logger = new FileLogger(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeepAwakeTool", "logs"));
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var configProvider = _sp.GetRequiredService<Func<AppConfig>>();
        while (!ct.IsCancellationRequested)
        {
            var cfg = configProvider();
            var interval = TimeSpan.FromSeconds(Math.Max(10, cfg.Activity.IntervalSeconds));
            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { break; }
            try { await _scheduler.RunOneTickAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.Log("ERROR", "Engine tick failed: " + ex); }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        try { _loop?.Wait(2000); } catch { /* faulted/cancelled loop is fine on shutdown */ }
        _cts?.Dispose();
    }
}
