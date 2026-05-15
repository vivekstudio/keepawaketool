using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class PowerTabViewModel
{
    private readonly AppConfig _src;
    public PowerTabViewModel(AppConfig cfg) => _src = cfg;
    public PowerConfig Build() => _src.Power;
}
