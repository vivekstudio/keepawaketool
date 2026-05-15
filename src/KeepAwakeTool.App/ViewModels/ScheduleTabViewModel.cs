using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class ScheduleTabViewModel
{
    private readonly AppConfig _src;
    public ScheduleTabViewModel(AppConfig cfg) => _src = cfg;
    public ScheduleConfig Build() => _src.Schedule;
}
