using System.Windows;
using SunnyBar.App.ViewModels;
namespace SunnyBar.App.Views;

public sealed class ScheduleWindow : Window
{
    public ScheduleEditorView Editor { get; }
    public ScheduleWindow(ScheduleEditorViewModel editor)
    {
        Icon=SunnyBar.App.Services.AppIcon.WindowImage;
        Title="Sunny Bar — 日程"; Width=1000; Height=850; MinWidth=860; MinHeight=760;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        DataContext=editor; Content=Editor=new(editor);
        Editor.CloseRequested+=(_,_)=>Close();
        Closed+=(_,_)=>Editor.Dispose();
    }
}
