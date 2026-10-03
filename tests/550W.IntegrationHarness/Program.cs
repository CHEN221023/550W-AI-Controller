using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Controller550W.Core;
namespace Controller550W.IntegrationHarness;

// Development fixture owns every process it starts. No UI injection or target-client inspection.
internal static class Program
{
    static readonly string Root=Environment.GetEnvironmentVariable("550W_TEST_ROOT")??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..",".."));
    static readonly string Source=Environment.GetEnvironmentVariable("550W_TEST_SOURCE")??(File.Exists(Path.Combine(Root,"550W-AI-Controller.sln"))?Root:Path.Combine(Root,"outputs","550W-AI-Controller-Source"));
    static readonly string Binary=Environment.GetEnvironmentVariable("550W_TEST_BINARY")??Path.Combine(Root,"outputs","550W-AI-Controller-V2-Portable");
    static readonly string Controller=Path.Combine(Binary,"550W-AI-Controller.exe");
    static readonly string Fixture=Path.Combine(Source,"tests","550W.Fixture","bin","Release","net8.0-windows","550W.ExternalFixture.exe");
    static readonly string Evidence=Path.Combine(Root,"work","desktop-v2-evidence",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);
    static Process? interactiveWatcher,interactiveTarget;static string interactiveData="";static TextBox text=null!;
    [DllImport("user32.dll")]static extern bool IsZoomed(nint hwnd);
    [STAThread]static void Main()
    {
        var folder=Path.GetFileName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));var manual=folder.StartsWith("desktop-manual",StringComparison.Ordinal);var extras=folder.StartsWith("desktop-extras",StringComparison.Ordinal);var focused=folder.StartsWith("desktop-focused",StringComparison.Ordinal);var shortcut=folder.StartsWith("desktop-shortcut",StringComparison.Ordinal);var identity=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AppContext.BaseDirectory)))[..12];using var single=new Mutex(true,"Local\\550W-v2-integration-"+(manual||extras||focused||shortcut?identity:"isolated"),out var first);if(!first)return;
        var app=new Application();var window=new Window{Title="550W V2 Integration Tests",Width=760,Height=530};var dock=new DockPanel();var buttons=new WrapPanel{Margin=new Thickness(12)};
        void Button(string caption,Action action){var b=new Button{Content=caption,Padding=new Thickness(10,7,10,7),Margin=new Thickness(4)};b.Click+=(_,_)=>action();buttons.Children.Add(b);}
        var token=new CancellationTokenSource();Button("运行自动回归",()=>_=Run(token.Token));Button("准备关闭确认测试",()=>_=PrepareInteractive(false));Button("准备加载失败关闭测试",()=>_=PrepareInteractive(true));Button("请求关闭 / 重复请求",()=>CloseInteractive());Button("关闭测试进程",CleanupInteractive);
        DockPanel.SetDock(buttons,Dock.Top);dock.Children.Add(buttons);text=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(15)};dock.Children.Add(text);window.Content=dock;
        window.Loaded+=async(_,_)=>{if(extras)await RunExtras(token.Token);else if(manual){await PrepareInteractive(false);CloseInteractive();}else {if(focused)await CleanupOldHarnesses(token.Token);await Run(token.Token);}};window.Closed+=(_,_)=>{token.Cancel();CleanupInteractive();};app.Run(window);
    }
    static void Log(string message){text.AppendText(message+Environment.NewLine);text.ScrollToEnd();}
    static Process Start(string executable,params string[] arguments){var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};foreach(var a in arguments)info.ArgumentList.Add(a);return Process.Start(info)!;}
    static string[] Logs(string data)=>Directory.Exists(Path.Combine(data,"Logs"))?Directory.GetFiles(Path.Combine(data,"Logs"),"*.log").SelectMany(File.ReadAllLines).ToArray():[];
    static async Task WatcherReady(string data,CancellationToken token){for(int i=0;i<60&&!Logs(data).Any(x=>x.Contains("[WatcherStart]"));i++)await Task.Delay(100,token);await Task.Delay(500,token);}
    static ControllerSettings Config(string data,bool optimizedClose=false,bool confirm=true,string[]? options=null)
    {
        var c=new ControllerSettings{FirstRunComplete=true,StartWithWindows=false};c.Audio.SoundVolume=0;c.Audio.VoiceEnabled=false;
        c.Profiles.Add(new AppProfile{Id="fixture-a",DisplayName="Fixture AI",ProcessNames=["550W.ExternalFixture.exe"],ExecutablePath=Fixture,LaunchTarget=Fixture,LaunchArguments=string.Join(" ",options??["--lifetime","21000"]),OptimizedShutdown=optimizedClose,CloseConfirmation=confirm});new ConfigStore(data).Save(c);return c;
    }
    static Process Watcher(string data,bool missing=false)
    {
        var info=new ProcessStartInfo(Controller){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};foreach(var a in new[]{"--background","--no-startup","--data-dir",data})info.ArgumentList.Add(a);info.Environment["550W_CAPTURE_DIR"]=Path.Combine(data,"images");if(missing){var empty=Path.Combine(data,"no-runtime");Directory.CreateDirectory(empty);info.Environment["550W_WEBVIEW2_BROWSER_FOLDER"]=empty;}return Process.Start(info)!;
    }
    static bool running;
    static async Task Run(CancellationToken token)
    {
        if(running)return;running=true;Directory.CreateDirectory(Evidence);var results=new List<object>();
        var focused=AppContext.BaseDirectory.Contains("desktop-focused",StringComparison.OrdinalIgnoreCase);var cases=new[]{
            new Case("passive-no-post-shutdown",["--lifetime","18500"]),new Case("minimize-restore",["--minimize-cycle","--lifetime","19000"]),
            new Case("helper-dedup",["--helpers","3","--lifetime","18500"]),new Case("slow-ready-15s",["--busy","15000","--lifetime","4500"]),
            new Case("managed-shortcut-maximize",["--lifetime","18500"],Managed:true),new Case("optimized-close-disabled",["--lifetime","18500"],EnabledClose:true),
            new Case("confirmed-native-fallback",["--lifetime","60000"],ManagedClose:true),new Case("crash-no-post-shutdown",["--lifetime","900","--crash"],ExpectFinish:false),
            new Case("missing-webview",["--lifetime","6500"],MissingRuntime:true,ExpectFinish:false),new Case("watchdog",["--busy","20000","--lifetime","2000"],Watchdog:true,ExpectFinish:false),
            new Case("tray-hide-reopen",["--cycle-v2"],Boot:2),new Case("two-profile-queue",["--lifetime","36000","--title","Fixture A"],Boot:2,TwoApps:true)
        };if(focused)cases=cases.Where(s=>new[]{"minimize-restore","crash-no-post-shutdown","missing-webview","tray-hide-reopen","managed-shortcut-maximize"}.Contains(s.Name)).Concat(new[]{new Case("minimize-repeat-2",["--minimize-cycle","--lifetime","19000"]),new Case("minimize-repeat-3",["--minimize-cycle","--lifetime","19000"])}).ToArray();if(AppContext.BaseDirectory.Contains("desktop-shortcut-pair",StringComparison.OrdinalIgnoreCase))cases=[new Case("managed-simultaneous-launch",["--lifetime","18500"],Managed:true,Offset:0),new Case("managed-launch-and-close-lifecycle",["--lifetime","60000"],Managed:true,ManagedClose:true)];else if(AppContext.BaseDirectory.Contains("desktop-shortcut",StringComparison.OrdinalIgnoreCase))cases=cases.Where(s=>s.Managed).ToArray();try{foreach(var s in cases)
        {
            token.ThrowIfCancellationRequested();Log("Running "+s.Name+"…");var data=Path.Combine(Evidence,s.Name+"-"+DateTime.Now.ToString("HHmmss"));Directory.CreateDirectory(data);var title="Fixture-"+Guid.NewGuid().ToString("N")[..10];var options=s.Options.Concat(new[]{"--title",title}).ToArray();var c=Config(data,s.EnabledClose||s.ManagedClose,!s.ManagedClose,options);c.Profiles[0].WindowTitleMatch=title;
            if(s.Managed){c.Profiles[0].LaunchMode=LaunchMode.OptimizedShortcut;c.Profiles[0].AppLaunchOffsetMs=s.Offset;}if(s.Watchdog)c.Animation.BootWatchdogMs=5000;if(s.TwoApps){c.Profiles[0].WindowTitleMatch="Fixture A";c.Profiles.Add(new(){Id="fixture-b",DisplayName="Fixture B",ProcessNames=["550W.ExternalFixture.exe"],WindowTitleMatch="Fixture B"});}new ConfigStore(data).Save(c);
            var link=Path.Combine(data,"Fixture AI (550W).lnk");if(s.Managed){using var setup=Start(Controller,"--data-dir",data,"--create-shortcut","fixture-a","--shortcut-path",link);await setup.WaitForExitAsync(token);if(setup.ExitCode!=0)throw new IOException("Shortcut creation failed");}
            using var watcher=Watcher(data,s.MissingRuntime);Process? target=null,other=null;bool maximized=false;double shortcutToVisual=0;
            try
            {
                await WatcherReady(data,token);
                if(s.Managed){var origin=DateTimeOffset.UtcNow;Process.Start(new ProcessStartInfo(link){UseShellExecute=true})?.Dispose();for(int i=0;i<100&&target==null;i++){await Task.Delay(100,token);target=Process.GetProcessesByName("550W.ExternalFixture").FirstOrDefault(p=>p.MainWindowHandle!=0);}for(int i=0;i<30&&!Logs(data).Any(x=>x.Contains("[FIRST_VISUAL]"));i++)await Task.Delay(50,token);var mark=Logs(data).FirstOrDefault(x=>x.Contains("[FIRST_VISUAL]"));if(mark!=null)shortcutToVisual=(DateTimeOffset.Parse(mark.Split(' ')[0])-origin).TotalMilliseconds;}
                else target=Start(Fixture,options);
                if(s.TwoApps){await Task.Delay(250,token);other=Start(Fixture,"--lifetime","36500","--title","Fixture B");}
                if(s.ManagedClose&&!s.Managed){await Task.Delay(2000,token);for(int i=0;i<4;i++)Command(data,"close");}
                if(s.Managed){await Task.Delay(5000,token);target!.Refresh();maximized=IsZoomed(target.MainWindowHandle);}
                if(s.ManagedClose&&s.Managed){for(int i=0;i<200&&!Logs(data).Any(x=>x.Contains("[ANIMATION_FINISHED]"));i++)await Task.Delay(100,token);if(!Logs(data).Any(x=>x.Contains("[ANIMATION_FINISHED]")))throw new Exception("Boot did not complete before the paired managed close.");for(int i=0;i<4;i++)Command(data,"close");}
                await target!.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(47),token);if(other!=null)await other.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(45),token);
                await Task.Delay(s.ManagedClose?4400:1600,token);var logs=Logs(data);var boot=logs.Count(x=>x.Contains("[BootTrigger]"))+logs.Count(x=>x.Contains("[ManagedLaunchSession]"));var postShutdown=logs.Count(x=>x.Contains("[ShutdownTrigger]"));var finished=logs.Count(x=>x.Contains("[ANIMATION_FINISHED]"));
                var wm=Array.FindIndex(logs,x=>x.Contains("[WM_CLOSE]"));var visible=Array.FindIndex(logs,x=>x.Contains("[SHUTDOWN_OVERLAY_VISIBLE]"));var began=Array.FindIndex(logs,x=>x.Contains("[SHUTDOWN_BEGIN_CONFIRMED]"));
                var animationLeft=Process.GetProcessesByName("550W.Animation").Select(p=>{using(p)return p.Id;}).ToArray();watcher.Refresh();var cpu=watcher.TotalProcessorTime.TotalMilliseconds;await Task.Delay(2000,token);watcher.Refresh();
                var pass=postShutdown==0&&boot==(s.ManagedClose?1:s.Boot)&&(!s.ExpectFinish||s.ManagedClose||finished==s.Boot)&&animationLeft.Length==0&&(!s.Managed||maximized)&&(!s.ManagedClose||(visible>=0&&began>visible&&wm>began&&logs.Count(x=>x.Contains("[WM_CLOSE]"))==1));
                var result=new{s.Name,pass,boot,postShutdown,finished,maximized,shortcutToVisual,animationLeft,firstVisual=logs.Where(x=>x.Contains("[FIRST_VISUAL]")).ToArray(),webReady=logs.Where(x=>x.Contains("[WEB_READY]")).ToArray(),waiting=logs.Where(x=>x.Contains("[WAITING_LOOP_READY]")).ToArray(),memoryMB=Math.Round(watcher.WorkingSet64/1048576d,1),privateMB=Math.Round(watcher.PrivateMemorySize64/1048576d,1),idleCpuPercentOfOneCore=Math.Round((watcher.TotalProcessorTime.TotalMilliseconds-cpu)/2000*100,2),logicalProcessors=Environment.ProcessorCount,logs};results.Add(result);await File.WriteAllTextAsync(Path.Combine(Evidence,"native-v2-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}),token);Log(s.Name+(pass?" PASS":" FAIL")+$" · boot {boot}, post-shutdown {postShutdown}, completed {finished}");
            }finally{foreach(var p in new[]{target,other,watcher})if(p!=null){try{if(!p.HasExited)p.Kill(true);}catch{}if(p!=watcher)p.Dispose();}}
        }Log("Automatic regression completed.");}catch(Exception e){Log("ERROR "+e);}finally{running=false;}
    }
    static void Command(string data,string action)
    {var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(data).ToUpperInvariant())))[..18];Start(Path.Combine(Binary,"550W.Launcher.exe"),"--"+action+"-profile","fixture-a","--pipe","550W-command-"+id,"--controller",Controller,"--data-dir",data).Dispose();}
    static async Task RunExtras(CancellationToken token)
    {
        await CleanupOldHarnesses(token);
        Directory.CreateDirectory(Evidence);var results=new List<object>();var voiceOnly=AppContext.BaseDirectory.Contains("desktop-extras-voice",StringComparison.OrdinalIgnoreCase);
        async Task Test(string name,Func<Task<object>> action){try{var detail=await action();results.Add(new{name,pass=true,detail});Log(name+" PASS");}catch(Exception e){results.Add(new{name,pass=false,error=e.ToString()});Log(name+" FAIL "+e.Message);}await File.WriteAllTextAsync(Path.Combine(Evidence,"native-extra-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}),token);}
        if(!voiceOnly)await Test("cancel-confirmation-releases-preload",async()=>{
            var data=Path.Combine(Evidence,"cancel-confirmation");Directory.CreateDirectory(data);var title="Cancel-"+Guid.NewGuid().ToString("N")[..8];var cfg=Config(data,true,true);cfg.Profiles[0].WindowTitleMatch=title;new ConfigStore(data).Save(cfg);
            using var target=Start(Fixture,"--lifetime","45000","--title",title);await Task.Delay(1400,token);using var watcher=Watcher(data);try{
                await WatcherReady(data,token);for(int i=0;i<4;i++)Command(data,"close");for(int i=0;i<130&&!Logs(data).Any(x=>x.Contains("[ShutdownPreloadReady]"));i++)await Task.Delay(100,token);
                if(!Logs(data).Any(x=>x.Contains("[ShutdownPreloadReady]")))throw new Exception("Preload was not ready while confirmation was pending.");
                for(int i=0;i<3;i++)Command(data,"cancel-close");for(int i=0;i<50&&!Logs(data).Any(x=>x.Contains("[ShutdownCancelled]"));i++)await Task.Delay(100,token);await Task.Delay(500,token);var logs=Logs(data);
                if(target.HasExited||logs.Count(x=>x.Contains("[CloseConfirmationShown]"))!=1||!logs.Any(x=>x.Contains("[ShutdownCancelled]"))||logs.Any(x=>x.Contains("[WM_CLOSE]")||x.Contains("[SHUTDOWN_OVERLAY_VISIBLE]")||x.Contains("[SHUTDOWN_BEGIN]"))||Process.GetProcessesByName("550W.Animation").Length!=0)throw new Exception("Cancellation did not keep the target running and release its silent preload.");
                return new{confirmationCount=1,targetStillRunning=true,preloadReady=true,animationReleased=true,noFullScreen=true,noShutdownBegin=true,noNormalClose=true,logs};
            }finally{try{using var exit=Start(Controller,"--data-dir",data,"--exit-controller");await exit.WaitForExitAsync(token);await watcher.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(3),token);}catch{if(!watcher.HasExited)watcher.Kill(true);}if(!target.HasExited)target.Kill(true);}
        });
        foreach(var extension in new[]{"svg","webp"}.Where(_=>!voiceOnly))await Test("custom-core-"+extension,async()=>{var input=Path.Combine(Source,"tests","assets","core-fixture."+extension);var output=Path.Combine(Evidence,"converted-"+extension+".png");using var process=Start(Path.Combine(Binary,"550W.Animation.exe"),"--render-core","--input",input,"--output",output);await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(13),token);var png=File.ReadAllBytes(output);if(png.Length<100||png[0]!=137||png[1]!=80)throw new Exception("No decoded PNG was produced.");await Task.Delay(500,token);return new{process.ExitCode,bytes=png.Length,output};});
        if(!voiceOnly)await Test("native-caption-move-resize-maximize",async()=>{var output=Path.Combine(Evidence,"native-caption.json");using var target=Start(Fixture,"--caption-audit",output,"--controller-assembly",Path.Combine(Binary,"550W-AI-Controller.dll"));await target.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(10),token);using var doc=JsonDocument.Parse(File.ReadAllText(output));if(doc.RootElement.EnumerateArray().Any(x=>!x.GetProperty("reliable").GetBoolean()))throw new Exception("A native caption position was rejected.");return new{target.ExitCode,output,positions=doc.RootElement.Clone()};});
        await Test("offline-machine-voice-render-and-release",async()=>{var output=Path.Combine(Evidence,"native-voice.json");using var target=Start(Fixture,"--voice-audit",output,"--animation-assembly",Path.Combine(Binary,"550W.Animation.dll"));await target.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(10),token);using var doc=JsonDocument.Parse(File.ReadAllText(output));if(!doc.RootElement.GetProperty("available").GetBoolean()||!doc.RootElement.GetProperty("released").GetBoolean())throw new Exception("Offline voice was not rendered or released.");return new{target.ExitCode,output,result=doc.RootElement.Clone()};});
        await Test("native-webview-offline-voice-playback-and-release",async()=>{
            var data=Path.Combine(Evidence,"voice-webview");Directory.CreateDirectory(data);var cfg=Config(data);cfg.Audio.VoiceEnabled=true;cfg.Audio.VoiceVolume=0;cfg.Audio.SoundEnabled=false;cfg.Audio.VoiceName="";new ConfigStore(data).Save(cfg);using var watcher=Watcher(data);
            try{await WatcherReady(data,token);using var command=Start(Controller,"--data-dir",data,"--boot-demo");await command.WaitForExitAsync(token);for(int i=0;i<240&&!Logs(data).Any(x=>x.Contains("[ANIMATION_FINISHED]"));i++)await Task.Delay(100,token);await Task.Delay(600,token);var logs=Logs(data);var played=logs.Count(x=>x.Contains("[VOICE_PLAYING]"));var released=Process.GetProcessesByName("550W.Animation").Length==0&&Process.GetProcessesByName("550W.VoiceWorker").Length==0;if(played<2||!released||!logs.Any(x=>x.Contains("[ANIMATION_FINISHED]")))throw new Exception($"Native WebView voice: played={played}, released={released}");return new{played,released,voiceVolume=0,note="Real HTML Audio play/decode; muted test gain avoids disturbing the desktop.",logs};}
            finally{try{using var exit=Start(Controller,"--data-dir",data,"--exit-controller");await exit.WaitForExitAsync(token);await watcher.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(3),token);}catch{if(!watcher.HasExited)watcher.Kill(true);}}
        });
        Log("Native extra regression completed.");
    }
    static async Task CleanupOldHarnesses(CancellationToken token){foreach(var previous in Process.GetProcessesByName("550W.IntegrationHarness")){using(previous){if(previous.Id==Environment.ProcessId)continue;try{var path=previous.MainModule?.FileName??"";if(path.StartsWith(Path.Combine(Root,"work","desktop-"),StringComparison.OrdinalIgnoreCase)){previous.Kill(true);await previous.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(3),token);}}catch{}}}}
    static async Task PrepareInteractive(bool missing)
    {
        CleanupInteractive();Directory.CreateDirectory(Evidence);interactiveData=Path.Combine(Evidence,(missing?"interactive-missing-":"interactive-")+DateTime.Now.ToString("HHmmss"));Directory.CreateDirectory(interactiveData);var title="Interactive-"+Guid.NewGuid().ToString("N")[..8];var c=Config(interactiveData,true,true,["--lifetime","360000","--title",title]);c.Profiles[0].WindowTitleMatch=title;new ConfigStore(interactiveData).Save(c);interactiveTarget=Start(Fixture,"--lifetime","360000","--title",title);await Task.Delay(1300);interactiveWatcher=Watcher(interactiveData,missing);await WatcherReady(interactiveData,CancellationToken.None);Log("Interactive fixture ready. "+interactiveData);Log("请求关闭会打开真正的产品确认框。先取消，再重新请求并确认。日志验证覆盖画面 → Shutdown Begin → WM_CLOSE。");
    }
    static void CloseInteractive(){if(interactiveData.Length>0)for(int i=0;i<3;i++)Command(interactiveData,"close");}
    static void CleanupInteractive(){foreach(var p in new[]{interactiveTarget,interactiveWatcher})if(p!=null){try{if(!p.HasExited)p.Kill(true);}catch{}p.Dispose();}interactiveTarget=null;interactiveWatcher=null;}
    sealed record Case(string Name,string[] Options,int Boot=1,bool Managed=false,bool EnabledClose=false,bool ManagedClose=false,bool MissingRuntime=false,bool Watchdog=false,bool ExpectFinish=true,bool TwoApps=false,int Offset=300);
}
