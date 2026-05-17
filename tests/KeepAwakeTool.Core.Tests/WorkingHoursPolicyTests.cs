using System;
using FluentAssertions;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Scheduling;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class WorkingHoursPolicyTests
{
    private static DateTimeOffset MondayAt(int h, int m) =>
        new(2026, 5, 11, h, m, 0, TimeSpan.Zero);  // 2026-05-11 is a Monday

    private static DateTimeOffset SaturdayAt(int h, int m) =>
        new(2026, 5, 9, h, m, 0, TimeSpan.Zero);

    [Fact]
    public void Disabled_schedule_is_always_within_window()
    {
        var s = new ScheduleConfig { Enabled = false };
        WorkingHoursPolicy.IsWithinWindow(s, MondayAt(3, 0)).Should().BeTrue();
    }

    [Fact]
    public void Inside_time_window_on_allowed_day_returns_true()
    {
        var s = new ScheduleConfig
        {
            Enabled = true, StartTime = "09:00", EndTime = "18:00",
            Days = new[] { DayOfWeek.Monday }
        };
        WorkingHoursPolicy.IsWithinWindow(s, MondayAt(12, 0)).Should().BeTrue();
    }

    [Fact]
    public void Outside_time_window_returns_false()
    {
        var s = new ScheduleConfig
        {
            Enabled = true, StartTime = "09:00", EndTime = "18:00",
            Days = new[] { DayOfWeek.Monday }
        };
        WorkingHoursPolicy.IsWithinWindow(s, MondayAt(20, 0)).Should().BeFalse();
    }

    [Fact]
    public void Day_not_in_allowed_list_returns_false()
    {
        var s = new ScheduleConfig
        {
            Enabled = true, StartTime = "09:00", EndTime = "18:00",
            Days = new[] { DayOfWeek.Monday }
        };
        WorkingHoursPolicy.IsWithinWindow(s, SaturdayAt(12, 0)).Should().BeFalse();
    }

    [Fact]
    public void Start_equal_or_after_end_means_all_day_treated_as_always_in()
    {
        var s = new ScheduleConfig
        {
            Enabled = true, StartTime = "18:00", EndTime = "09:00",
            Days = new[] { DayOfWeek.Monday }
        };
        WorkingHoursPolicy.IsWithinWindow(s, MondayAt(3, 0)).Should().BeTrue();
    }
}
