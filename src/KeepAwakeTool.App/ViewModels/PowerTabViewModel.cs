using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class PowerTabViewModel
{
    public bool ForceDisplayOffAfterInjection { get; set; }
    public bool PowerSaveMode { get; set; }

    public PowerTabViewModel(AppConfig cfg)
    {
        ForceDisplayOffAfterInjection = cfg.Power.ForceDisplayOffAfterInjection;
        PowerSaveMode = cfg.Power.PowerSaveMode;
    }

    public PowerConfig Build() => new()
    {
        ForceDisplayOffAfterInjection = ForceDisplayOffAfterInjection,
        PowerSaveMode = PowerSaveMode
    };
}
