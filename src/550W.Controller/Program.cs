using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Controller550W.Controller.Services;
using Controller550W.Controller.Views;
using Forms = System.Windows.Forms;

namespace Controller550W.Controller;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        string? Option(string key) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var data = Option("--data-dir") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "550W AI Controller");
        data = Path.GetFullPath(data);
        var startupLog = new LogService(data);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => startupLog.Error("UnhandledException", e.ExceptionObject as Exception ?? new Exception(), crash: true);
        TaskScheduler.UnobservedTaskException += (_, e) => { startupLog.Error("UnobservedTaskException", e.Exception); e.SetObserved(); };
        if (Option("--export-diagnostics") is { } exportPath)
        {
            try { DiagnosticExportService.ExportAsync(exportPath, data, new ConfigStore(data).Load()).GetAwaiter().GetResult(); }
            catch (Exception ex) { startupLog.Error("DiagnosticsExportFailed", ex); Environment.ExitCode = 1; }
            return;
        }
        if (Option("--diagnostics-check") is { } report)
        {
            try
            {
                var log = new LogService(data); var rows = new List<DiagnosticRow>();
                Task.Run(() => new DiagnosticsService(log).RunAsync(new(), new(), row => { lock (rows) rows.Add(row); }, CancellationToken.None)).GetAwaiter().GetResult();
                File.WriteAllText(report, WireJson.Serialize(rows));
            }
            catch (Exception ex) { startupLog.Error("DiagnosticUnavailable", ex); Environment.ExitCode = 1; } return;
        }
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToUpperInvariant())))[..18];
        if(Option("--create-shortcut") is {} shortcutId&&Option("--shortcut-path") is {} shortcutPath)
        {try{var store=new ConfigStore(data);var config=store.Load();var p=config.Profiles.First(x=>x.Id==shortcutId);new ShortcutService(data,"550W-command-"+id).Create(p,shortcutPath);store.Save(config);}catch{Environment.ExitCode=1;}return;}
        using var mutex = new Mutex(true, @"Local\550W-" + id, out var first);
        if (!first)
        {
            var command = Option("--launch-profile") is {} launch ? "launch|"+launch+"|"+Stopwatch.GetTimestamp() : Option("--close-profile") is {} close ? "close|"+close : Option("--cancel-close-profile") is {} cancel ? "cancel-close|"+cancel : args.Contains("--exit-controller")?"exit":args.Contains("--boot-demo") ? "boot" : args.Contains("--shutdown-demo") ? "shutdown" : args.Contains("--background") ? "" : "settings";
            if (command.Length > 0) try { using var pipe = new NamedPipeClientStream(".", "550W-command-" + id, PipeDirection.Out, PipeOptions.CurrentUserOnly); pipe.Connect(600); using var writer = new StreamWriter(pipe); writer.WriteLine(command); } catch { }
            return;
        }
        try
        {
            startupLog.Write("ControllerStart");
            var app = new ControllerApplication(data, id, args); app.Run();
        }
        catch (Exception e) { startupLog.Error("StartupFailed", e, crash: true); MessageBox.Show("550W 控制器启动失败。错误已记录，请导出诊断包以便排查。", "550W", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
}

internal sealed class ControllerApplication(string data, string id, string[] args) : Application
{
    ConfigStore store = null!; ControllerSettings config = null!; LogService log = null!; ApplicationMonitor monitor = null!; AnimationCoordinator animation = null!;
    OptimizedLaunchService launcher=null!;ManagedCloseService managedClose=null!;CaptionProxyService captionProxy=null!;
    Forms.NotifyIcon tray = null!; SettingsWindow? settingsWindow; readonly CancellationTokenSource life = new();
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        log = new(data);
        DispatcherUnhandledException += (_, ex) => { ex.Handled = true; log.Error("UiError", ex.Exception, crash: true); tray?.ShowBalloonTip(3000, "550W", "操作未完成，后台继续监控。可在“关于”中导出故障日志。", Forms.ToolTipIcon.Info); };
        store = new(data); config = store.Load();
        monitor = new(config, log); animation = new(() => config, monitor, log, data);
        launcher=new(()=>config,monitor,animation,log);managedClose=new(()=>config,monitor,animation,log);captionProxy=new(()=>config,monitor,managedClose,log);
        monitor.AnimationRequested += (p, kind, hwnd, origin) => animation.RequestAnimation(p, kind, hwnd,origin:origin);
        monitor.TargetStopped += p=>animation.CancelProfile(p.Id);
        animation.Notice += text => Dispatcher.BeginInvoke(() => tray.ShowBalloonTip(3500, "550W Controller", text, Forms.ToolTipIcon.Info));
        tray = new Forms.NotifyIcon { Text = "550W AI Controller", Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "controller.ico")), Visible = true };
        tray.DoubleClick += (_, _) => ShowSettings();
        tray.ContextMenuStrip = TrayMenu(); monitor.Start();
        if (!args.Contains("--no-startup")) try { StartupService.SetEnabled(config.StartWithWindows); } catch (Exception ex) { log.Write("StartupUnavailable", ex.GetType().Name); }
        var firstRun = !config.FirstRunComplete; config.FirstRunComplete = true; store.Save(config);
        _ = Commands();
        if (args.Contains("--settings") || (firstRun && !args.Contains("--background"))) ShowSettings();
        if (args.Contains("--boot-demo")) Demo(AnimationKind.Boot);
        if (args.Contains("--shutdown-demo")) Demo(AnimationKind.Shutdown);
        string? Opt(string key){var i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
        if(Opt("--launch-profile") is {} launch)Execute("launch|"+launch+"|"+Stopwatch.GetTimestamp());
        if(Opt("--close-profile") is {} close)Execute("close|"+close);
        if(args.Contains("--exit-controller"))Shutdown();
        var at = Array.IndexOf(args, "--exit-after");
        if (at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out var ms))
        { var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; timer.Tick += (_, _) => { timer.Stop(); Shutdown(); }; timer.Start(); }
        if (store.LoadWarning != null) { log.Warning("ConfigWarning"); tray.ShowBalloonTip(3500, "550W", store.LoadWarning, Forms.ToolTipIcon.Info); }
    }
    Forms.ContextMenuStrip TrayMenu()
    {
        var menu = new Forms.ContextMenuStrip(); menu.Items.Add("550W AI Controller").Enabled = false; menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("打开设置", null, (_, _) => ShowSettings());
        var apps = new Forms.ToolStripMenuItem("当前监控应用"); menu.Items.Add(apps);
        var actions=new Forms.ToolStripMenuItem("550W 启动 / 关闭应用");menu.Items.Add(actions);
        var paused = new Forms.ToolStripMenuItem("暂停动画") { CheckOnClick = true, Checked = config.Paused }; paused.Click += (_, _) => { config.Paused = paused.Checked; store.Save(config); }; menu.Items.Add(paused);
        menu.Items.Add("预览启动动画", null, (_, _) => Demo(AnimationKind.Boot)); menu.Items.Add("预览关闭动画", null, (_, _) => Demo(AnimationKind.Shutdown));
        menu.Items.Add("重新扫描 AI", null, (_, _) => { ShowSettings(); if (settingsWindow != null) _ = settingsWindow.Scan(); });
        menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("退出", null, (_, _) => Shutdown());
        menu.Opening += (_, _) => { paused.Checked = config.Paused; apps.DropDownItems.Clear();actions.DropDownItems.Clear(); foreach (var p in config.Profiles) {apps.DropDownItems.Add(p.DisplayName + " · " + p.RuntimeStatus).Enabled = false;actions.DropDownItems.Add("启动 "+p.DisplayName+"（550W）",null,(_,_)=>_=launcher.Launch(p));actions.DropDownItems.Add("关闭 "+p.DisplayName+"（550W）",null,(_,_)=>Dispatcher.BeginInvoke(()=>managedClose.Request(p))).Enabled=monitor.FindWindow(p)!=0;} if (config.Profiles.Count == 0) apps.DropDownItems.Add("尚未添加应用").Enabled = false; };
        return menu;
    }
    void Demo(AnimationKind kind) => animation.RequestAnimation(new AppProfile { DisplayName = "演示应用", MinimumBootTimeMs = config.Animation.MinimumBootMs }, kind, demo: true);
    void ShowSettings()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(ShowSettings); return; }
        if (settingsWindow != null) { settingsWindow.Show(); if (settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState = WindowState.Normal; settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow(config, updated =>
        {
            if (!args.Contains("--no-startup")) StartupService.SetEnabled(updated.StartWithWindows);
            store.Save(updated); config = updated; monitor.Configure(config);captionProxy.Configure(); log.Write("SettingsSaved", $"profiles={config.Profiles.Count}");
        }, () => config,data,"550W-command-"+id);
        settingsWindow.Preview += (updated, profile, kind, audioTest) => animation.RequestAnimation(profile, kind, demo: true, audioTest: audioTest, previewSettings: updated);
        settingsWindow.ManagedCloseRequested+=p=>{var actual=config.Profiles.FirstOrDefault(x=>x.Id==p.Id);if(actual!=null)managedClose.Request(actual);};
        settingsWindow.DebugRequested+=(updated,p)=>animation.RequestAnimation(p,AnimationKind.Boot,demo:true,previewSettings:updated,debug:true);
        settingsWindow.Closed += (_, _) => settingsWindow = null; settingsWindow.Show();
    }
    async Task Commands()
    {
        while (!life.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream("550W-command-" + id, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(life.Token); using var reader = new StreamReader(pipe); var command = await reader.ReadLineAsync(life.Token);
                await Dispatcher.InvokeAsync(() => Execute(command??""));
            }
            catch (OperationCanceledException) { return; } catch (IOException ex) { log.Error("CommandUnavailable", ex); }
        }
    }
    void Execute(string command)
    {
        if(command=="exit"){Shutdown();return;}if(command=="settings"){ShowSettings();return;}if(command=="boot"){Demo(AnimationKind.Boot);return;}if(command=="shutdown"){Demo(AnimationKind.Shutdown);return;}
        var parts=command.Split('|');if(parts.Length<2)return;var p=config.Profiles.FirstOrDefault(x=>x.Id==parts[1]);if(p==null){log.Write("ProfileUnavailable",parts[1]);return;}
        if(parts[0]=="launch"){long origin=0;if(parts.Length>2&&long.TryParse(parts[2],out var tick)&&tick>0&&tick<=Stopwatch.GetTimestamp()&&Stopwatch.GetElapsedTime(tick)<TimeSpan.FromSeconds(15))origin=tick;_=launcher.Launch(p,origin);}
        else if(parts[0]=="close")managedClose.Request(p);
        else if(parts[0]=="cancel-close")managedClose.CancelRequest(p);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        life.Cancel();captionProxy?.Dispose();managedClose?.Dispose(); monitor?.Dispose(); animation?.Dispose();
        if (tray != null) { tray.Visible = false; tray.Icon?.Dispose(); tray.Dispose(); }
        log?.Write("WatcherStop"); log?.Write("ControllerStop"); base.OnExit(e);
    }
}
