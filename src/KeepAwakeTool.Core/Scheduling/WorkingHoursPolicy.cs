using System;
using System.Globalization;
using System.Linq;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.Core.Scheduling;

public static class WorkingHoursPolicy
{
    public static bool IsWithinWindow(ScheduleConfig schedule, DateTimeOffset now)
    {
        if (!schedule.Enabled) return true;
        if (!schedule.Days.Contains(now.DayOfWeek)) return false;

        if (!TimeSpan.TryParseExact(schedule.StartTime, "hh\\:mm", CultureInfo.InvariantCulture, out var start)) return true;
        if (!TimeSpan.TryParseExact(schedule.EndTime,   "hh\\:mm", CultureInfo.InvariantCulture, out var end))   return true;
        if (start >= end) return true; // misconfig: treat as all-day

        var current = now.TimeOfDay;
        return current >= start && current < end;
    }
}
