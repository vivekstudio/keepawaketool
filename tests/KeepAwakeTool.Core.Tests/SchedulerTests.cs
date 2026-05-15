using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Scheduling;
using KeepAwakeTool.Core.Tests.Fakes;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class SchedulerTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeInputSimulator _input = new();
    private readonly FakeIdleMonitor _idle = new() { Value = TimeSpan.FromMinutes(5) };
    private readonly FakePowerManager _power = new();

    [Fact]
    public void State_starts_as_Stopped()
    {
        var engine = new ActivityEngine(_input, _idle, _power, _clock, ConfigDefaults.Default());
        var sched = new Scheduler(engine, () => ConfigDefaults.Default(), _clock);
        sched.State.Should().Be(EngineState.Stopped);
    }

    [Fact]
    public void Start_transitions_to_Running()
    {
        var engine = new ActivityEngine(_input, _idle, _power, _clock, ConfigDefaults.Default());
        var sched = new Scheduler(engine, () => ConfigDefaults.Default(), _clock);
        sched.Start();
        sched.State.Should().Be(EngineState.Running);
    }

    [Fact]
    public void TogglePause_round_trips_Running_and_Paused()
    {
        var engine = new ActivityEngine(_input, _idle, _power, _clock, ConfigDefaults.Default());
        var sched = new Scheduler(engine, () => ConfigDefaults.Default(), _clock);
        sched.Start();
        sched.TogglePause();
        sched.State.Should().Be(EngineState.Paused);
        sched.TogglePause();
        sched.State.Should().Be(EngineState.Running);
    }

    [Fact]
    public async Task RunOneTick_respects_schedule_window()
    {
        // Saturday at 03:00 — outside Mon-Fri 09-18
        _clock.SetLocal(new DateTimeOffset(2026, 5, 9, 3, 0, 0, TimeSpan.Zero));

        var cfg = ConfigDefaults.Default() with
        {
            Schedule = new ScheduleConfig
            {
                Enabled = true, StartTime = "09:00", EndTime = "18:00",
                Days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            }
        };
        var engine = new ActivityEngine(_input, _idle, _power, _clock, cfg);
        var sched = new Scheduler(engine, () => cfg, _clock);
        sched.Start();
        await sched.RunOneTickAsync(CancellationToken.None);
        _input.MouseMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOneTick_respects_PowerSave_mode()
    {
        var cfg = ConfigDefaults.Default() with { Power = new PowerConfig { PowerSaveMode = true } };
        var engine = new ActivityEngine(_input, _idle, _power, _clock, cfg);
        var sched = new Scheduler(engine, () => cfg, _clock);
        sched.Start();
        sched.ApplyConfig(cfg);
        await sched.RunOneTickAsync(CancellationToken.None);
        sched.State.Should().Be(EngineState.PowerSave);
        _input.MouseMoves.Should().BeEmpty();
    }
}
