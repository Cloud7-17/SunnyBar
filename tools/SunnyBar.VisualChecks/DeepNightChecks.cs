using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunnyBar.App.Controls;
using SunnyBar.Core;

internal static class DeepNightChecks
{
    internal static void Run(string output)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var clock = new Clock();
        var snapshot = new TimelineService(clock, new Solar()).Capture(zone, new(39.9042,116.4074));
        if (snapshot.Solar?.DeepNight?.Count != 2) throw new Exception("Expected split deep-night bands");
        var compact = Control(snapshot,1600,32,true);
        var baseline = Control(snapshot with { Solar=snapshot.Solar with { DeepNight=[] } },1600,32,true);
        var pixels = Pixels(compact,1600,32); var plain = Pixels(baseline,1600,32);
        var changed=0;
        var visible=0;
        for(var y=0;y<32;y++)for(var x=0;x<1600;x++)
        {
            var index=(y*1600+x)*4;
            if(pixels.Skip(index).Take(4).SequenceEqual(plain.Skip(index).Take(4)))continue;
            changed++;
            if(Enumerable.Range(0,3).Max(channel=>(int)pixels[index+channel]-plain[index+channel])>=24)visible++;
            if(y<16 || !snapshot.Solar.DeepNight.Any(segment=>x+1>=segment.StartProgress*1600 && x<=segment.EndProgress*1600))
                throw new Exception("Star pixels escaped deep-night bar segments");
        }
        if(visible<80)throw new Exception("Deep-night stars are too faint or sparse");
        Program.Save(compact,Brushes.WhiteSmoke,1600,32,Path.Combine(output,"deep-night-compact.png"));
        var detailed=Control(snapshot,1200,140,false);
        Program.Save(detailed,Brushes.WhiteSmoke,1200,140,Path.Combine(output,"deep-night-timeline.png"));
        var noMidnight=Control(snapshot with { Midnight=null },1600,32,true);
        var noMarker=Pixels(noMidnight,1600,32);
        var midnightX=new TimeAxisMapper().ToX(snapshot.Midnight!.Value,snapshot.Window,0,1600);
        var markerChanged=false;
        for(var y=0;y<16;y++)for(var x=(int)midnightX-7;x<(int)midnightX+7;x++)
        {
            var index=(y*1600+x)*4;
            if(!pixels.Skip(index).Take(4).SequenceEqual(noMarker.Skip(index).Take(4)))markerChanged=true;
        }
        if(!markerChanged)throw new Exception("Midnight star marker missing");

        var live=Control(snapshot,1200,32,true);
        var window=new Window { Content=live,Width=1200,Height=80,Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false };
        window.Show(); window.UpdateLayout();
        Pixels(live,1200,32);
        if(live.IsTwinkling!=SystemParameters.ClientAreaAnimation)throw new Exception("Animation preference was ignored");
        var first=Pixels(live,1200,32);
        Pump(TimeSpan.FromMilliseconds(1300));
        var second=Pixels(live,1200,32);
        if(SystemParameters.ClientAreaAnimation && first.SequenceEqual(second))throw new Exception("Twinkle frames did not change");
        var frameDelta=first.Zip(second,(left,right)=>Math.Abs((int)left-right)).Sum();
        if(SystemParameters.ClientAreaAnimation && frameDelta<3000)throw new Exception("Twinkle brightness variation is not visible enough");
        Program.Save(live,Brushes.WhiteSmoke,1200,32,Path.Combine(output,"deep-night-animation-frame.png"));
        window.Hide();
        if(live.IsTwinkling)throw new Exception("Hidden window kept twinkling");
        window.Show(); window.UpdateLayout();
        live.Snapshot=snapshot with { Solar=snapshot.Solar with { DeepNight=[] } };
        Pixels(live,1200,32);
        if(live.IsTwinkling)throw new Exception("No-star timeline kept animation timer running");
        live.Snapshot=snapshot; Pixels(live,1200,32); window.Close();
        if(live.IsTwinkling)throw new Exception("Closed window kept twinkling");
        File.WriteAllText(Path.Combine(output,"deep-night-result.txt"),$"PASS: midnight marker, deep-night-only pixels, visible stars ({visible} pixels, {changed} total changed), animation variation ({frameDelta} channel levels), system animation={SystemParameters.ClientAreaAnimation}, hidden/no-star/closed timer stop. No user files changed.");
    }
    private static TimelineControl Control(TimelineSnapshot snapshot,int width,int height,bool compact)
    {
        var control=new TimelineControl { Snapshot=snapshot,IsCompact=compact };
        control.Measure(new Size(width,height));control.Arrange(new Rect(0,0,width,height));control.UpdateLayout();return control;
    }
    private static byte[] Pixels(Visual control,int width,int height)
    {
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(control);
        var bytes=new byte[width*height*4];bitmap.CopyPixels(bytes,width*4,0);return bytes;
    }
    private static void Pump(TimeSpan duration)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer { Interval=duration };
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    private sealed class Clock:IClock { public DateTimeOffset UtcNow=>new(2026,10,28,8,0,0,TimeSpan.Zero); }
    private sealed class Solar:ISolarEventCalculator
    {
        public SolarCalculation Calculate(DateTimeOffset start,DateTimeOffset end,GeoLocation location)=>new(false,
            [new(new(2026,10,28,6,30,0,TimeSpan.FromHours(8)),SolarEventKind.Sunrise),new(new(2026,10,28,17,30,0,TimeSpan.FromHours(8)),SolarEventKind.Sunset)]);
    }
}
