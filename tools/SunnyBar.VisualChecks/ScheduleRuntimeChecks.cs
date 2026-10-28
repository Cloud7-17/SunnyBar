using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SunnyBar.App.Services;
using SunnyBar.App.ViewModels;
using SunnyBar.App.Views;
using SunnyBar.Core;

internal static class ScheduleRuntimeChecks
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window,out Rect rect);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint window,int message,nint wParam,nint lParam);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    private static nint Point(int x,int y)=>new((y<<16)|(x&0xffff));
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
        var handle=new WindowInteropHelper(bar).Handle;
        if(!GetWindowRect(handle,out var bounds))throw new Exception("Cannot read bar bounds");
        var scale=GetDpiForWindow(handle)/96.0;
        var x=bounds.Left+100; var y=bounds.Top+(int)(20*scale);
        if(SendMessage(handle,0x84,0,Point(x,y))!=1 || SendMessage(handle,0x84,0,Point(x,bounds.Top+1))!=-1)throw new Exception("Visible-bar hit testing or empty-area pass-through failed");
        if(SendMessage(handle,0x21,0,0)!=3)throw new Exception("Bar activates on a single click");
        SendMessage(handle,0x201,1,Point(100,(int)(20*scale)));
        if(application.Windows.OfType<EditorWindow>().Any())throw new Exception("Single click opened editor");
        SendMessage(handle,0x202,0,Point(100,(int)(20*scale)));
        SendMessage(handle,0x201,1,Point(100,(int)(20*scale)));
        var window=application.Windows.OfType<EditorWindow>().Single();
        var editorHandle=new WindowInteropHelper(window).Handle;
        if(window.Icon is null || !window.ShowInTaskbar ||
            (SendMessage(editorHandle,0x7f,0,0)==0 && SendMessage(editorHandle,0x7f,1,0)==0 && SendMessage(editorHandle,0x7f,2,0)==0))
            throw new Exception("Editor taskbar window has no native icon");
        runtime.OpenSchedule(); runtime.OpenSchedule();
        if(application.Windows.OfType<EditorWindow>().Count()!=1)throw new Exception("Duplicate editor");
        var editor=(ScheduleEditorViewModel)window.ScheduleEditor.DataContext;
        editor.Selected=editor.Events.Single(); editor.Title="编辑后立即生效";
        if(!editor.Save()||model.Snapshot!.Schedule!.Current.Single().Title!="编辑后立即生效")throw new Exception("Runtime did not share schedule updates");
        Program.Render(window,1200,830,Path.Combine(output,"editor-schedules.png"));
        editor.Title="切页保留草稿";
        runtime.OpenSettings();
        if(!ReferenceEquals(application.Windows.OfType<EditorWindow>().Single(),window))throw new Exception("Settings opened another editor");
        Program.Render(window,1200,830,Path.Combine(output,"editor-markers.png"));
        var settingsEditor=(SettingsEditorViewModel)window.SettingsEditor.DataContext;
        settingsEditor.AddPreset(0);
        var save=Descendants(window.SettingsEditor).OfType<Button>().Single(item=>Equals(item.Content,"保存并应用"));
        save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(settings.Load().Settings.DailyMarkers.Single().Label!="上班")throw new Exception("Embedded marker save failed");
        var navigation=(ListBox)window.FindName("Navigation");
        navigation.SelectedIndex=4;
        Program.Render(window,1200,830,Path.Combine(output,"editor-appearance.png"));
        navigation.SelectedIndex=5;
        window.Width=1060; window.Height=800; window.UpdateLayout();
        var content=(FrameworkElement)window.Content;
        Program.Render(window,(int)content.ActualWidth,(int)content.ActualHeight,Path.Combine(output,"editor-small.png"));
        runtime.OpenSchedule();
        if(editor.Title!="切页保留草稿")throw new Exception("Switching pages discarded schedule draft");
        var close=Descendants(window.ScheduleEditor).OfType<Button>().Single(item=>Equals(item.Content,"关闭"));
        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(!bar.IsVisible || application.Windows.OfType<EditorWindow>().Any())throw new Exception("Editor close did not keep bar running");
        runtime.OpenSettings(); runtime.Dispose();
        if(application.Windows.Count!=0)throw new Exception("Runtime leaked a window");
        application.Shutdown();
        File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: isolated native hit tests, single click ignored, double click opens editor, one shared editor, navigation, draft retention, schedule and marker save, close/reopen and full disposal. No user files or startup registration changed.");
        Console.WriteLine("PASS: isolated editor navigation and native double-click lifecycle");
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
    private sealed class NoStartup:IStartupRegistration{public void Apply(bool enabled,Action persistSettings)=>persistSettings();}
}
