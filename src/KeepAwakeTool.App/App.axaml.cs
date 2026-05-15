using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KeepAwakeTool.App.Composition;
using System;

namespace KeepAwakeTool.App;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = default!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ServiceRegistration.Build();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // TODO(Task 22): create and initialize TrayIconController here
        }
        base.OnFrameworkInitializationCompleted();
    }
}
