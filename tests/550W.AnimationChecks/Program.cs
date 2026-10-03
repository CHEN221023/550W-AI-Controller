using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Controller550W.Core;

// Runs only inside the dedicated noninteractive desktop launcher. Never switch desktops.
internal static class Program
{
    [DllImport("user32.dll")] static extern nint GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetUserObjectInformation(nint handle,int index,StringBuilder info,int length,out int needed);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll")] static extern nint GetWindowLongPtr(nint window,int index);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out int process);
    delegate bool EnumProc(nint window,nint arg);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,nint arg);
    static string output="";static int exitCode;
    [STAThread] static int Main(string[] args)
    {
        output=args[1];var desktopName=new StringBuilder(256);GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()),2,desktopName,512,out _);
        if(!desktopName.ToString().StartsWith("550W-V41-",StringComparison.Ordinal)&&!args.Contains("--offscreen")){File.WriteAllText(output,"{\"error\":\"Use isolated desktop or explicit offscreen fixture\"}");return 2;}
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Startup+=async(_,_)=>{try{await Check(args[0],desktopName.ToString());}catch(Exception ex){exitCode=1;File.WriteAllText(output,JsonSerializer.Serialize(new{pass=false,error=ex.ToString()}));}finally{app.Shutdown();}};app.Run();return exitCode;
    }
    static async Task Check(string binary,string desktop)
    {
        File.AppendAllText(output+".trace","fixture started\n");var target=new Window{Title="V4.1 target fixture",Width=900,Height=650,Background=Brushes.Magenta,Left=-30000,Top=-30000,ShowActivated=false};target.Show();File.AppendAllText(output+".trace","target shown\n");var targetHwnd=new WindowInteropHelper(target).Handle;
        using var life=new CancellationTokenSource(TimeSpan.FromSeconds(45));var id=Guid.NewGuid().ToString("N");
        var session=new AnimationSession{SessionId=id,Kind=AnimationKind.Boot,DisplayName="Isolated target fixture",Theme="550W",DataDirectory=Path.GetDirectoryName(output)!,MonitorX=-30000,MonitorY=-30000,MonitorWidth=640,MonitorHeight=360,MinimumBootTimeMs=0,Audio=new AudioSettings{Muted=true,SoundEnabled=false,VoiceEnabled=false},Settings=new AnimationSettings{CoreHoldMs=900,CoreDissolveMs=350,ParticleResidueMs=200,LogoRevealMs=400,LogoHoldMs=300,LogoBurstMs=400,MultiWindowRevealMs=350,DurationMultiplier=1,BootWatchdogMs=30000,ReadyMs=100,FadeMs=200,HandoffCompressMs=200},CoreImagePath=Path.Combine(binary,"Themes","550W","assets","core.png")};
        using var pipe=new NamedPipeServerStream("550W-"+id,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        var info=new ProcessStartInfo(Path.Combine(binary,"550W.Animation.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};info.ArgumentList.Add("--pipe");info.ArgumentList.Add("550W-"+id);info.ArgumentList.Add("--parent");info.ArgumentList.Add(Environment.ProcessId.ToString());
        using var host=Process.Start(info)!;File.AppendAllText(output+".trace","host started "+host.Id+"\n");
        try{
            await pipe.WaitForConnectionAsync(life.Token);File.AppendAllText(output+".trace","pipe connected\n");using var reader=new StreamReader(pipe,Encoding.UTF8,false,4096,true);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true};
            await writer.WriteLineAsync(WireJson.Serialize(new{type="init",session}));
            nint overlay=0;var events=new List<string>();int coverChecks=0,raises=0;bool handingOff=false;var violations=new List<string>();
            void FindOverlay(){EnumWindows((hwnd,_)=>{GetWindowThreadProcessId(hwnd,out var pid);if(pid==host.Id&&IsWindowVisible(hwnd))overlay=hwnd;return true;},0);}
            bool AboveTarget(){for(var h=GetWindow(targetHwnd,3);h!=0;h=GetWindow(h,3))if(h==overlay)return true;return false;}
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(8)};timer.Tick+=(_,_)=>{if(overlay==0||handingOff)return;coverChecks++;if(!IsWindowVisible(overlay))violations.Add("hidden overlay");if(((long)GetWindowLongPtr(overlay,-20)&8)==0)violations.Add("lost topmost");if(!AboveTarget())violations.Add("target above overlay");};
            while(await reader.ReadLineAsync(life.Token) is {} line){using var doc=JsonDocument.Parse(line);var type=doc.RootElement.GetProperty("type").GetString()!;events.Add(type);File.AppendAllText(output+".trace",type+"\n");
                if(type=="VISUAL_READY"){await writer.WriteLineAsync(WireJson.Serialize(new{type="target",target=new{processDetected=true,windowDetected=true,ready=true}}));FindOverlay();if(overlay==0)throw new Exception("Missing visible overlay");timer.Start();}
                if(type is "CORE_REVEAL" or "CORE_PARTICLES" or "LOGO_ASSEMBLING" or "LOGO_BURST" or "MULTIWINDOW_REVEAL"){
                    SetWindowPos(targetHwnd,0,0,0,0,0,0x13);raises++;
                }
                if(type=="HANDOFF_BEGIN"){handingOff=true;timer.Stop();}
                if(type=="ANIMATION_FINISHED")break;
                if(type is "error" or "FATAL" or "WATCHDOG")throw new Exception(line);
            }
            timer.Stop();await host.WaitForExitAsync(life.Token);target.Close();
            if(violations.Count>0||coverChecks<30||raises!=5||!events.Contains("ANIMATION_FINISHED"))throw new Exception($"Coverage failures={violations.Count}; checks={coverChecks}; raises={raises}; completed={events.Contains("ANIMATION_FINISHED")}");
            File.WriteAllText(output,JsonSerializer.Serialize(new{pass=true,desktop,interactiveDesktopShown=false,coverChecks,foregroundChallenges=raises,violations,events,hostExitCode=host.ExitCode},new JsonSerializerOptions{WriteIndented=true}));
        }finally{if(!host.HasExited)host.Kill(true);target.Close();}
    }
}
