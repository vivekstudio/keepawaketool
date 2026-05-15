using System;
using System.Threading;
using System.Threading.Tasks;
using KeepAwakeTool.Core.Activity;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakeClock : IClock
{
    private DateTimeOffset _now = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    public TimeSpan LastDelay { get; private set; } = TimeSpan.Zero;
    public DateTimeOffset UtcNow => _now;
    public DateTimeOffset LocalNow => _now;
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        LastDelay = delay;
        _now = _now.Add(delay);
        return Task.CompletedTask;
    }
    public void Advance(TimeSpan by) => _now = _now.Add(by);
    public void SetLocal(DateTimeOffset value) => _now = value;
}
