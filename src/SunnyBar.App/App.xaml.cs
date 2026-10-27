using System.Windows;
using SunnyBar.App.Services;

namespace SunnyBar.App;

public partial class App : Application
{
    private SingleInstanceGuard? instance;
    private DesktopRuntime? runtime;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            instance = new SingleInstanceGuard();
            if (!instance.IsOwner)
            {
                SingleInstanceGuard.NotifyExistingInstance(e.Args.Contains("--preview"));
                Shutdown();
                return;
            }
            runtime = new DesktopRuntime(this);
            runtime.Start(e.Args.Contains("--preview"));
        }
        catch (Exception error)
        {
            MessageBox.Show($"Sunny Bar 无法启动：{error.Message}", "Sunny Bar", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        runtime?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
