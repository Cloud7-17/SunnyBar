using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunnyBar.App.Services;
using SunnyBar.App.ViewModels;
using SunnyBar.App.Views;
using SunnyBar.Core;

internal static class ScheduleRuntimeChecks
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var settings=new SettingsService(Path.Combine(output,"data-"+Guid.NewGuid().ToString("N")));
        settings.Save(new());
        var events=new ScheduleStore(settings.DirectoryPath);
        events.Save(new(){Events=[new(){Title="生命周期测试",StartAt=DateTimeOffset.Now.AddMinutes(-30),EndAt=DateTimeOffset.Now.AddMinutes(30)}]});
        var application=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        using var runtime=new DesktopRuntime(application,settings,new NoStartup());
        runtime.Start();
        var bar=(BarWindow)application.MainWindow;
        var model=(TimelineViewModel)bar.DataContext;
        if(model.Snapshot!.Schedule!.Current.Count!=1)throw new Exception("Runtime did not load events on startup");
        runtime.OpenSchedule(); runtime.OpenSchedule();
        var window=application.Windows.OfType<ScheduleWindow>().Single();
        var editor=(ScheduleEditorViewModel)window.DataContext;
        editor.Selected=editor.Events.Single(); editor.Title="编辑后立即生效";
        if(!editor.Save()||model.Snapshot!.Schedule!.Current.Single().Title!="编辑后立即生效")throw new Exception("Runtime did not share schedule updates");
        window.Close();
        if(!bar.IsVisible)throw new Exception("Closing schedule hid bar");
        runtime.OpenSettings();
        var settingsWindow=application.Windows.OfType<SettingsWindow>().Single();
        var button=Descendants((DependencyObject)settingsWindow.Content).OfType<Button>().Single(item=>Equals(item.Content,"管理日程…"));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(application.Windows.OfType<ScheduleWindow>().Count()!=1)throw new Exception("Settings schedule entry not connected");
        settingsWindow.Close();
        runtime.Dispose();
        if(application.Windows.Count!=0)throw new Exception("Runtime leaked a window");
        application.Shutdown();
        File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: isolated DesktopRuntime event loading, one schedule window, edit sharing, close-continues-running, settings entry and full window disposal. No user files or startup registration changed.");
        Console.WriteLine("PASS: isolated schedule desktop lifecycle");
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
    private sealed class NoStartup:IStartupRegistration{public void Apply(bool enabled,Action persistSettings)=>persistSettings();}
}
