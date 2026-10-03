using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text;

namespace Controller550W.Fixture;

internal static class Program
{
    [DllImport("user32.dll")]static extern uint GetDpiForWindow(nint hwnd);
    [STAThread] static void Main(string[] args)
    {
        int Number(string key, int fallback) { var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length&&int.TryParse(args[i+1],out var n)?n:fallback; }
        string Text(string key,string fallback) { var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback; }
        var lifetime=Number("--lifetime",9000);
        if(args.Contains("--helper")){Thread.Sleep(lifetime);return;}
        for(var i=0;i<Number("--helpers",0);i++){var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add("--helper");start.ArgumentList.Add("--lifetime");start.ArgumentList.Add(lifetime.ToString());Process.Start(start)?.Dispose();}
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var window=new Window{Title=Text("--title","550W External AI Fixture"),Width=820,Height=520,Background=new SolidColorBrush(Color.FromRgb(225,228,232)),Content=new TextBlock{Text="EXTERNAL AI WINDOW\n\nReadiness / minimize / tray-hide fixture\n\nThis process contains no controller code.",FontSize=27,Margin=new Thickness(40)}};
        window.Closed+=(_,_)=>app.Shutdown();
        app.Startup+=async(_,_)=>{
            await Task.Delay(Number("--delay",0));window.Show();
            if(Text("--voice-audit","") is {Length:>0} voiceAudit){try{var assembly=Assembly.LoadFrom(Text("--animation-assembly",""));var type=assembly.GetType("Controller550W.Animation.VoiceService")!;var configType=Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(Text("--animation-assembly",""))!,"550W.Core.dll")).GetType("Controller550W.Core.AudioSettings")!;var config=Activator.CreateInstance(configType)!;configType.GetProperty("VoiceEnabled")!.SetValue(config,true);var service=Activator.CreateInstance(type,[config])!;var clips=new List<object>();foreach(var phrase in new[]{"550W 系统就绪。","DeepSeek interface ready"}){var task=(Task)type.GetMethod("RenderOfflineAsync")!.Invoke(service,[phrase,Path.GetDirectoryName(voiceAudit)!,CancellationToken.None])!;await task;var file=(string?)task.GetType().GetProperty("Result")!.GetValue(task);if(file==null||!File.Exists(file))throw new Exception("Offline machine voice did not produce a WAV.");var bytes=File.ReadAllBytes(file);if(bytes.Length<100||Encoding.ASCII.GetString(bytes,0,4)!="RIFF")throw new Exception("Invalid PCM result.");clips.Add(new{phrase,file,bytes=bytes.Length});}((IDisposable)service).Dispose();var released=type.GetField("voice",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)==null&&Process.GetProcessesByName("550W.VoiceWorker").Length==0;File.WriteAllText(voiceAudit,JsonSerializer.Serialize(new{available=true,engine="eSpeak NG 1.52.0",released,clips},new JsonSerializerOptions{WriteIndented=true}));}catch(Exception e){File.WriteAllText(voiceAudit,JsonSerializer.Serialize(new{available=false,released=false,error=e.ToString()},new JsonSerializerOptions{WriteIndented=true}));}window.Close();return;}
            if(Text("--caption-audit","") is {Length:>0} audit){var assembly=Assembly.LoadFrom(Text("--controller-assembly",""));var method=assembly.GetType("Controller550W.Controller.Services.CaptionProxyService")!.GetMethod("TryBounds",BindingFlags.Static|BindingFlags.Public)!;var positions=new List<object>();foreach(var phase in new[]{"normal","moved","resized","maximized"}){if(phase=="moved"){window.Left=120;window.Top=110;}if(phase=="resized"){window.Width=1020;window.Height=630;}if(phase=="maximized")window.WindowState=WindowState.Maximized;await Task.Delay(400);var hwnd=new System.Windows.Interop.WindowInteropHelper(window).Handle;object?[] values=[hwnd,null];var reliable=(bool)method.Invoke(null,values)!;positions.Add(new{phase,reliable,dpi=GetDpiForWindow(hwnd),bounds=values[1]});}File.WriteAllText(audit,JsonSerializer.Serialize(positions,new JsonSerializerOptions{WriteIndented=true}));window.Close();return;}
            if(Number("--busy",0)>0)Thread.Sleep(Number("--busy",0));
            if(args.Contains("--minimize-cycle")){await Task.Delay(6000);window.WindowState=WindowState.Minimized;await Task.Delay(1000);window.WindowState=WindowState.Normal;await Task.Delay(lifetime-7000);window.Close();app.Shutdown();}
            else if(args.Contains("--cycle-v2")){await Task.Delay(18000);window.Hide();await Task.Delay(3200);window.Show();await Task.Delay(18500);window.Close();app.Shutdown();}
            else if(args.Contains("--cycle")){await Task.Delay(5000);window.WindowState=WindowState.Minimized;await Task.Delay(1200);window.WindowState=WindowState.Normal;await Task.Delay(1800);window.Hide();await Task.Delay(3200);window.Show();await Task.Delay(5200);window.Close();app.Shutdown();}
            else{await Task.Delay(lifetime);if(args.Contains("--crash"))Environment.Exit(13);window.Close();app.Shutdown();}
        };app.Run();
    }
}
