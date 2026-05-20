using System;
using System.IO;
using KeepAwakeTool.App;
using KeepAwakeTool.App.Notifications;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Diagnostics;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Core.Power;
using KeepAwakeTool.Core.Scheduling;
#if WINDOWS
using KeepAwakeTool.Platform.Win;
#else
using KeepAwakeTool.Platform.Mac;
#endif
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App.Composition;

public static class ServiceRegistration
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeepAwakeTool", "config.json");

        services.AddSingleton(new FileLogger(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeepAwakeTool", "logs")));

        var store = new ConfigurationStore(configPath);
        var initial = store.Load();
        store.StartWatching();
        services.AddSingleton(store);

        AppConfig configSnapshot = initial;
        store.Changed += (_, cfg) => configSnapshot = cfg;
        services.AddSingleton<Func<AppConfig>>(_ => () => configSnapshot);

        services.AddSingleton<IClock, SystemClock>();

#if WINDOWS
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
        services.AddSingleton<IInputSimulator, WindowsInputSimulator>();
        services.AddSingleton<IIdleMonitor, WindowsIdleMonitor>();
        services.AddSingleton<IPowerManager, WindowsPowerManager>();
        services.AddSingleton<IAutoStartManager>(_ => new WindowsAutoStartManager(exePath));
        services.AddSingleton<ISessionInfo, WindowsSessionInfo>();
        services.AddSingleton<IGlobalHotkeyService>(sp =>
        {
            var fl = sp.GetRequiredService<KeepAwakeTool.Core.Diagnostics.FileLogger>();
            return new WindowsGlobalHotkeyService((level, msg) => fl.Log(level, msg));
        });
        services.AddSingleton<IPermissionGate, WindowsPermissionGate>();
        services.AddSingleton<IInputPermissionPrompt, WindowsInputPermissionPrompt>();
        services.AddSingleton<ISystemPowerEvents, WindowsSystemPowerEvents>();
#else
        services.AddSingleton<IInputSimulator, MacInputSimulator>();
        services.AddSingleton<IIdleMonitor, MacIdleMonitor>();
        services.AddSingleton<IPermissionGate, MacPermissionGate>();
        services.AddSingleton<IInputPermissionPrompt, MacInputPermissionPrompt>();
        services.AddSingleton<ISessionInfo, MacSessionInfo>();
        services.AddSingleton<IPowerManager>(sp =>
        {
            var fl = sp.GetRequiredService<KeepAwakeTool.Core.Diagnostics.FileLogger>();
            return new MacPowerManager((lvl, msg) => fl.Log(lvl, msg));
        });
        services.AddSingleton<IAutoStartManager>(sp =>
        {
            var fl = sp.GetRequiredService<KeepAwakeTool.Core.Diagnostics.FileLogger>();
            return new MacAutoStartManager((lvl, msg) => fl.Log(lvl, msg));
        });
        services.AddSingleton<ISystemPowerEvents, MacSystemPowerEvents>();
        services.AddSingleton<IGlobalHotkeyService>(sp =>
        {
            var fl = sp.GetRequiredService<KeepAwakeTool.Core.Diagnostics.FileLogger>();
            return new MacGlobalHotkeyService((lvl, msg) => fl.Log(lvl, msg));
        });
#endif

        services.AddSingleton<ActivityEngine>(sp => new ActivityEngine(
            sp.GetRequiredService<IInputSimulator>(),
            sp.GetRequiredService<IIdleMonitor>(),
            sp.GetRequiredService<IPowerManager>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<Func<AppConfig>>()(),
            sp.GetRequiredService<IPermissionGate>()));

        services.AddSingleton<Scheduler>(sp => new Scheduler(
            sp.GetRequiredService<ActivityEngine>(),
            sp.GetRequiredService<Func<AppConfig>>(),
            sp.GetRequiredService<IClock>()));

        services.AddSingleton<PowerModeController>(sp => new PowerModeController(sp.GetRequiredService<IPowerManager>()));

        services.AddSingleton<EnginePump>(sp => new EnginePump(sp, sp.GetRequiredService<KeepAwakeTool.Core.Diagnostics.FileLogger>()));

        services.AddSingleton<ToastService>();

        return services.BuildServiceProvider();
    }
}
