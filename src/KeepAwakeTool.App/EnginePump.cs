using System;
using System.Threading;
using System.Threading.Tasks;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App;

public sealed class EnginePump : IDisposable
{
    private readonly IServiceProvider _sp;
    private readonly Scheduler _scheduler;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public EnginePump(IServiceProvider sp)
    {
        _sp = sp;
        _scheduler = sp.GetRequiredService<Scheduler>();
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
            catch (TaskCanceledException) { break; }
            try { await _scheduler.RunOneTickAsync(ct); }
            catch (Exception) { /* logged in Task 29 */ }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loop?.Wait(2000); } catch { }
        _cts?.Dispose();
    }
}
