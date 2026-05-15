using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class ActivityTabViewModel
{
    private readonly AppConfig _src;
    public ActivityTabViewModel(AppConfig cfg) => _src = cfg;
    public ActivityConfig Build() => _src.Activity;
}
