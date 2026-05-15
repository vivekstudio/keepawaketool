using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KeepAwakeTool.App.Composition;
using KeepAwakeTool.App.Tray;
using System;

namespace KeepAwakeTool.App;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = default!;
    public TrayIconController? Tray { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ServiceRegistration.Build();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Tray = new TrayIconController(Services);
            Tray.Initialize();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
