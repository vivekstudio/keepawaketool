using System;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakeIdleMonitor : IIdleMonitor
{
    public TimeSpan Value { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan TimeSinceLastUserInput() => Value;
}
