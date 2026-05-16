using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Tests.Fakes;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class ActivityEngineInjectedTests
{
    [Fact]
    public async Task Injected_fires_when_input_is_emitted()
    {
        var input = new FakeInputSimulator();
        var idle = new FakeIdleMonitor { Value = TimeSpan.FromMinutes(10) };
        var power = new FakePowerManager();
        var clock = new FakeClock();
        var engine = new ActivityEngine(input, idle, power, clock, ConfigDefaults.Default());
        int fired = 0;
        engine.Injected += () => fired++;
        await engine.TickAsync(CancellationToken.None);
        fired.Should().Be(1);
    }

    [Fact]
    public async Task Injected_does_not_fire_when_gated_by_powersave()
    {
        var input = new FakeInputSimulator();
        var idle = new FakeIdleMonitor { Value = TimeSpan.FromMinutes(10) };
        var power = new FakePowerManager();
        var clock = new FakeClock();
        var cfg = ConfigDefaults.Default() with { Power = new PowerConfig { PowerSaveMode = true } };
        var engine = new ActivityEngine(input, idle, power, clock, cfg);
        int fired = 0;
        engine.Injected += () => fired++;
        await engine.TickAsync(CancellationToken.None);
        fired.Should().Be(0);
    }
}
