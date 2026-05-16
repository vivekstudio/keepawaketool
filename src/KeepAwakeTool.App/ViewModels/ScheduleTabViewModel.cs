using System;
using System.Collections.Generic;
using System.Linq;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class ScheduleTabViewModel : ObservableObject
{
    private bool _enabled;
    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "18:00";
    public bool Mon { get; set; } = true;
    public bool Tue { get; set; } = true;
    public bool Wed { get; set; } = true;
    public bool Thu { get; set; } = true;
    public bool Fri { get; set; } = true;
    public bool Sat { get; set; }
    public bool Sun { get; set; }

    public ScheduleTabViewModel(AppConfig cfg)
    {
        Enabled = cfg.Schedule.Enabled;
        StartTime = cfg.Schedule.StartTime;
        EndTime = cfg.Schedule.EndTime;
        var days = cfg.Schedule.Days;
        Mon = days.Contains(DayOfWeek.Monday);
        Tue = days.Contains(DayOfWeek.Tuesday);
        Wed = days.Contains(DayOfWeek.Wednesday);
        Thu = days.Contains(DayOfWeek.Thursday);
        Fri = days.Contains(DayOfWeek.Friday);
        Sat = days.Contains(DayOfWeek.Saturday);
        Sun = days.Contains(DayOfWeek.Sunday);
    }

    public ScheduleConfig Build()
    {
        var days = new List<DayOfWeek>();
        if (Mon) days.Add(DayOfWeek.Monday);
        if (Tue) days.Add(DayOfWeek.Tuesday);
        if (Wed) days.Add(DayOfWeek.Wednesday);
        if (Thu) days.Add(DayOfWeek.Thursday);
        if (Fri) days.Add(DayOfWeek.Friday);
        if (Sat) days.Add(DayOfWeek.Saturday);
        if (Sun) days.Add(DayOfWeek.Sunday);
        return new ScheduleConfig
        {
            Enabled = Enabled, StartTime = StartTime, EndTime = EndTime, Days = days
        };
    }
}
