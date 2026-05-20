using Avalonia;
using KeepAwakeTool.Core.Platform;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KeepAwakeTool.App;

internal static class Program
{
    private static ISingleInstanceGuard? _guard;

    [STAThread]
    public static int Main(string[] args)
    {
        var logger = new KeepAwakeTool.Core.Diagnostics.FileLogger(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeepAwakeTool", "logs"));

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.Log("FATAL", e.ExceptionObject?.ToString() ?? "unknown");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Log("ERROR", e.Exception.ToString());
            e.SetObserved();
        };

        _guard = CreateGuard();
        if (!_guard.TryAcquire())
        {
            _guard.SignalExistingInstance();
            return 0;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        finally
        {
            _guard?.Release();
        }
    }

    private static ISingleInstanceGuard CreateGuard()
    {
#if WINDOWS
        return new KeepAwakeTool.Platform.Win.WindowsSingleInstanceGuard();
#else
        // macOS implementation lands in Task 18 (Phase 2).
        throw new PlatformNotSupportedException("macOS single-instance guard lands in Phase 2.");
#endif
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
