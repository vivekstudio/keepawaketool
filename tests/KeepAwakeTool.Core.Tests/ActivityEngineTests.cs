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

    [Fact]
    public async Task Tick_skips_when_user_recently_active()
    {
        _idle.Value = TimeSpan.FromSeconds(5);   // less than default 30s threshold
        var engine = BuildEngine();
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task Tick_skips_when_PowerSaveMode_on()
    {
        var cfg = ConfigDefaults.Default() with { Power = new PowerConfig { PowerSaveMode = true } };
        var engine = BuildEngine(cfg);
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().BeEmpty();
        _power.ForceDisplayOffCalls.Should().Be(0);
    }

    [Fact]
    public async Task Tick_skips_when_hotkey_paused()
    {
        var engine = BuildEngine();
        engine.HotkeyPaused = true;
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task Tick_skips_when_outside_working_hours()
    {
        var engine = BuildEngine();
        engine.WithinWorkingHours = false;
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().BeEmpty();
    }
}
