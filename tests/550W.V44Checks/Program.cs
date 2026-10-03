using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using Controller550W.Core;
using Controller550W.Controller.Services;
using Controller550W.Rendering;

// No displayed windows, no input injection, no interaction with installed applications.
internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        var report=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        try
        {
            var clock=Stopwatch.StartNew();using var particles=ParticleFirstFrame.Create(new());var rasterMs=clock.Elapsed.TotalMilliseconds;
            using var frame=new Bitmap(1920,1080);using(var g=Graphics.FromImage(frame)){g.Clear(Color.Black);g.DrawImageUnscaled(particles,704,348);}
            var lit=0;for(var y=0;y<1080;y++)for(var x=0;x<1920;x++){var c=frame.GetPixel(x,y);if(c.R+c.G+c.B>0){lit++;if(x<750||x>1170||y<390||y>690)throw new Exception("First-frame particles left existing central bounds");}}
            if(lit<1500)throw new Exception("Missing central particles");
            var png=Path.ChangeExtension(report,"png");frame.Save(png,ImageFormat.Png);
            var monitor=CheckProcessFirst(args[1],Path.Combine(Path.GetDirectoryName(report)!,"watcher-data")).GetAwaiter().GetResult();
            File.WriteAllText(report,JsonSerializer.Serialize(new{pass=true,rasterMs,litPixels=lit,pureBlackOutsideCenter=true,noWindowsShown=true,monitor},new JsonSerializerOptions{WriteIndented=true}));return 0;
        }
        catch(Exception e){File.WriteAllText(report,JsonSerializer.Serialize(new{pass=false,error=e.ToString()},new JsonSerializerOptions{WriteIndented=true}));return 1;}
    }
    static async Task<object> CheckProcessFirst(string fixture,string data)
    {
        var name=Path.GetFileNameWithoutExtension(fixture);if(Process.GetProcessesByName(name).Length!=0)throw new Exception("Test fixture already running");
        var p=new AppProfile{Id="v44",DisplayName="Latency fixture",ProcessNames=[name],StartTrigger=StartTrigger.WindowShow};
        var config=new ControllerSettings{FirstRunComplete=true,Profiles=[p]};using var monitor=new ApplicationMonitor(config,new LogService(data));
        var appeared=new TaskCompletionSource<(long Tick,nint Window)>(TaskCreationOptions.RunContinuationsAsynchronously);var stopped=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var count=0;
        monitor.AnimationRequested+=(profile,kind,window,origin)=>{Interlocked.Increment(ref count);appeared.TrySetResult((Stopwatch.GetTimestamp(),window));};monitor.TargetStopped+=_=>stopped.TrySetResult();monitor.Start();
        var started=Stopwatch.GetTimestamp();var info=new ProcessStartInfo(fixture){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};info.ArgumentList.Add("--helper");info.ArgumentList.Add("--lifetime");info.ArgumentList.Add("1500");
        using var target=Process.Start(info)!;var result=await appeared.Task.WaitAsync(TimeSpan.FromSeconds(3));await target.WaitForExitAsync();await stopped.Task.WaitAsync(TimeSpan.FromSeconds(3));
        if(count!=1||result.Window!=0)throw new Exception("Process-first boot repeated or waited for a window");
        return new{triggerMs=Stopwatch.GetElapsedTime(started,result.Tick).TotalMilliseconds,triggers=count,windowRequired=false,stopObserved=true};
    }
}
