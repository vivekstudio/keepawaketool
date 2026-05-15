using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Tests.Fakes;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class ActivityEngineTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeInputSimulator _input = new();
    private readonly FakeIdleMonitor _idle = new() { Value = TimeSpan.FromMinutes(5) };
    private readonly FakePowerManager _power = new();

    private ActivityEngine BuildEngine(AppConfig? cfg = null)
        => new(_input, _idle, _power, _clock, cfg ?? ConfigDefaults.Default());

    [Fact]
    public async Task Tick_moves_mouse_when_user_is_idle()
    {
        var engine = BuildEngine();
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().ContainSingle()
            .Which.mode.Should().Be(MouseMode.Invisible);
    }
}
