using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;
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
    public async Task Tick_skips_when_idle_below_interval()
    {
        _idle.Value = TimeSpan.FromSeconds(30);  // less than default 60s interval
        var engine = BuildEngine();
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task Tick_injects_when_idle_equals_interval()
    {
        _idle.Value = TimeSpan.FromSeconds(60);  // exactly the default 60s interval
        var engine = BuildEngine();
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().ContainSingle();
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

    [Fact]
    public async Task Keystroke_fires_only_on_every_Nth_tick()
    {
        var cfg = ConfigDefaults.Default() with
        {
            Activity = ConfigDefaults.Default().Activity with
            {
                Keystroke = new KeystrokeConfig { Enabled = true, Key = VirtualKey.F15, EveryNthCycle = 3 }
            }
        };
        var engine = BuildEngine(cfg);
        for (int i = 0; i < 6; i++) await engine.TickAsync(CancellationToken.None);

        _input.Keys.Should().HaveCount(2);
        _input.Keys.Should().AllSatisfy(k => k.Should().Be(VirtualKey.F15));
    }

    [Fact]
    public async Task Keystroke_skipped_when_disabled()
    {
        var cfg = ConfigDefaults.Default() with
        {
            Activity = ConfigDefaults.Default().Activity with
            {
                Keystroke = new KeystrokeConfig { Enabled = false, Key = VirtualKey.F15, EveryNthCycle = 1 }
            }
        };
        var engine = BuildEngine(cfg);
        for (int i = 0; i < 3; i++) await engine.TickAsync(CancellationToken.None);
        _input.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task S1_ForceDisplayOff_runs_after_200ms_delay()
    {
        var cfg = ConfigDefaults.Default() with
        {
            Power = new PowerConfig { ForceDisplayOffAfterInjection = true }
        };
        var engine = BuildEngine(cfg);
        await engine.TickAsync(CancellationToken.None);

        _input.MouseMoves.Should().ContainSingle();
        _clock.LastDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        _power.ForceDisplayOffCalls.Should().Be(1);
    }

    [Fact]
    public async Task S1_ForceDisplayOff_skipped_when_disabled()
    {
        var engine = BuildEngine();
        await engine.TickAsync(CancellationToken.None);
        _power.ForceDisplayOffCalls.Should().Be(0);
    }
}
