using System.Windows;
using SunnyBar.App.ViewModels;
namespace SunnyBar.App.Views;

public sealed class SettingsWindow : Window
{
    public SettingsEditorView Editor { get; }
    public event EventHandler? PreviewRequested;
    public event EventHandler? ScheduleRequested;
    public SettingsWindow(SettingsEditorViewModel editor)
    {
        Icon=SunnyBar.App.Services.AppIcon.WindowImage;
        Title="Sunny Bar — 设置"; Width=820; Height=740; MinWidth=720; MinHeight=620;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        DataContext=editor; Content=Editor=new(editor);
        Editor.CloseRequested+=(_,_)=>Close();
        Editor.PreviewRequested+=(_,_)=>PreviewRequested?.Invoke(this,EventArgs.Empty);
        Editor.ScheduleRequested+=(_,_)=>ScheduleRequested?.Invoke(this,EventArgs.Empty);
    }
}
