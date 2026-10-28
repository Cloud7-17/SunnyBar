using System.Windows;
using System.Windows.Controls;
using SunnyBar.App.ViewModels;
namespace SunnyBar.App.Views;
public sealed class ScheduleImportWindow:Window
{
    public ScheduleImportWindow(ScheduleEditorViewModel editor)
    {
        Icon=SunnyBar.App.Services.AppIcon.WindowImage;
        Title="粘贴导入日程";Width=780;Height=620;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var grid=new DockPanel{Margin=new Thickness(18)};Content=grid;
        var tip=new TextBlock{Text="粘贴 AI 生成的 JSON。校验通过后确认追加，不替换已有日程。",Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(tip,Dock.Top);grid.Children.Add(tip);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Bottom);grid.Children.Add(actions);
        var input=new TextBox{AcceptsReturn=true,AcceptsTab=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new System.Windows.Media.FontFamily("Consolas"),Text=FormatTemplate};
        var copy=new Button{Content="复制格式模板",Margin=new Thickness(6),Padding=new Thickness(10)};actions.Children.Add(copy);copy.Click+=(_,_)=>Clipboard.SetText(FormatTemplate);
        var import=new Button{Content="校验并导入",Margin=new Thickness(6),Padding=new Thickness(10)};actions.Children.Add(import);
        import.Click+=(_,_)=>
        {
            var text=input.Text;
            try
            {
                var events=editor.PreviewImport(text);
                var preview=string.Join("\n",events.Take(12).Select(item=>$"{item.Title}  {item.StartAt:yyyy-MM-dd HH:mm zzz} → {item.EndAt:MM-dd HH:mm}  {(item.Repeat is null?"单次":"每周 "+string.Join(",",item.Repeat.Days))}"));
                if(MessageBox.Show(this,$"追加 {events.Count} 条日程？\n{preview}\n重复导入会再次添加。","导入预览",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                if(editor.Import(text))Close();else MessageBox.Show(this,editor.Status);
            }
            catch(Exception error) when(error is System.Text.Json.JsonException or FormatException or SunnyBar.Core.SettingsValidationException or ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
            {MessageBox.Show(this,"未导入："+error.Message);}
        };
        grid.Children.Add(input);
    }
    public const string FormatTemplate="""
{"version":1,"timeZone":"Asia/Shanghai","events":[
  {"title":"上课","date":"2026-11-02","start":"08:00","end":"09:35","days":[1,3,5]},
  {"title":"整理笔记","date":"2026-11-02","start":"20:00","end":"21:00"}
]}
""";
}
