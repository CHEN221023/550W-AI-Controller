using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Controller550W.Rendering;

namespace Controller550W.Animation;

internal sealed class AnimationWindow : Window
{
    readonly AnimationSession session; readonly int parent; readonly StreamReader reader; readonly StreamWriter writer;
    readonly CancellationTokenSource life = new(); readonly Grid grid = new(); readonly Grid core = new();
    readonly List<string> buffered = []; readonly DispatcherTimer watchdog = new(), parentWatch = new();
    WebView2CompositionControl? web; bool pageReady, finishing, activated, visualCommitted;OverlayCoverGuard? coverGuard; readonly object writeLock = new(); readonly VoiceService voice;
    readonly Dictionary<string,string> audioPaths=new(StringComparer.OrdinalIgnoreCase);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern nint GetWindowLongPtrW(nint hwnd,int index);
    [DllImport("user32.dll")] static extern nint SetWindowLongPtrW(nint hwnd,int index,nint value);
    public AnimationWindow(AnimationSession session, int parent, StreamReader reader, StreamWriter writer)
    {
        this.session = session; this.parent = parent; this.reader = reader; this.writer = writer;
        voice = new VoiceService(session.Audio);
        Title = "550W Animation"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        AllowsTransparency = true; Background = Brushes.Black; Topmost = !session.Preload; ShowActivated = !session.Preload;
        // The existing native cover keeps animating while this view prepares in parallel.
        Opacity = session.Preload || session.ParticleStartTimestamp>0 ? 0 : 1; IsHitTestVisible = !session.Preload;
        Width = session.MonitorWidth; Height = session.MonitorHeight; Content = grid;
        if(session.Kind!=AnimationKind.Boot)
        {
            var image = new Image { Source = MechanicalCore.Load(session.CoreImagePath), Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            core.Children.Add(image);
            SizeChanged += (_,_) => { var scale=Math.Min(ActualWidth/1920,ActualHeight/1080); image.Width=250*scale; image.Height=330*scale; };
        }
        grid.Children.Add(core);
        SourceInitialized += (_, _) => {if(session.Preload){var hwnd=new WindowInteropHelper(this).Handle;SetWindowLongPtrW(hwnd,-20,(nint)((long)GetWindowLongPtrW(hwnd,-20)|0x080000A0));}Position();if(session.Kind==AnimationKind.Boot)coverGuard=new OverlayCoverGuard(new WindowInteropHelper(this).Handle,()=>!finishing&&(!session.Preload||activated));};
        Deactivated+=(_,_)=>coverGuard?.Reassert();
        Loaded += async (_, _) => { Position(); Send(new { type = "FIRST_FRAME" }); _ = ReadMessages(); await InitializeWeb(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && session.Settings.AllowEscape) { e.Handled = true; Send(new { type = "USER_SKIP" }); Finish(); } };
        watchdog.Interval = TimeSpan.FromMilliseconds(session.Preload ? 120000 : session.Kind == AnimationKind.Boot ? session.Settings.BootWatchdogMs : session.Settings.ShutdownWatchdogMs + 5000);
        watchdog.Tick += (_, _) => { Send(new { type = "WATCHDOG" }); Close(); }; watchdog.Start();
        parentWatch.Interval = TimeSpan.FromMilliseconds(600); parentWatch.Tick += (_, _) => { try { using var p = Process.GetProcessById(parent); if (p.HasExited) Close(); } catch { Close(); } }; parentWatch.Start();
        Closed += (_, _) => { life.Cancel();coverGuard?.Dispose(); watchdog.Stop(); parentWatch.Stop(); voice.Dispose(); web?.Dispose(); };
    }
    void Position() => SetWindowPos(new WindowInteropHelper(this).Handle, session.Preload && !activated ? 0 : new nint(-1), session.MonitorX, session.MonitorY, session.MonitorWidth, session.MonitorHeight, 0x40 | (session.Preload && !activated ? 0x10u : 0));
    void Send(object message)
    {
        try { lock (writeLock) writer.WriteLine(WireJson.Serialize(message)); } catch { }
    }
    async Task ReadMessages()
    {
        try
        {
            while (await reader.ReadLineAsync(life.Token) is { } line)
                await Dispatcher.InvokeAsync(() =>
                {
                    using var msg=JsonDocument.Parse(line); var type=msg.RootElement.GetProperty("type").GetString();
                    if(type=="cancel"){Close();return;}
                    if(type=="cover"){coverGuard?.Reassert();return;}
                    if(type=="activate"&&!activated){activated=true;var hwnd=new WindowInteropHelper(this).Handle;SetWindowLongPtrW(hwnd,-20,(nint)((long)GetWindowLongPtrW(hwnd,-20)&~0x08000020L));Opacity=1;Topmost=true;IsHitTestVisible=true;Position();Activate();watchdog.Stop();watchdog.Interval=TimeSpan.FromMilliseconds(session.Settings.ShutdownWatchdogMs);watchdog.Start();}
                    if (pageReady) web!.CoreWebView2.PostWebMessageAsJson(line); else buffered.Add(line);
                });
            if (!life.IsCancellationRequested) await Dispatcher.InvokeAsync(Close);
        }
        catch (OperationCanceledException) { } catch { if (!life.IsCancellationRequested) await Dispatcher.InvokeAsync(Close); }
    }
    string ThemePage()
    {
        if (!Regex.IsMatch(session.Theme, "^[A-Za-z0-9_-]{1,64}$")) throw new IOException("Invalid theme name");
        var root = System.IO.Path.Combine(AppContext.BaseDirectory, "Themes", session.Theme);
        using var manifest = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(root, "theme.json")));
        var page = session.DebugMode ? "DebugDiagnostics.html" : manifest.RootElement.GetProperty(session.Kind == AnimationKind.Boot ? "bootPage" : "shutdownPage").GetString()!;
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, page));
        if (!full.StartsWith(System.IO.Path.GetFullPath(root) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) throw new IOException("Invalid theme page");
        return full;
    }
    async Task InitializeWeb()
    {
        try
        {
            var page = ThemePage();
            var browserFolder=Environment.GetEnvironmentVariable("550W_WEBVIEW2_BROWSER_FOLDER");
            try { CoreWebView2Environment.GetAvailableBrowserVersionString(browserFolder); }
            catch (WebView2RuntimeNotFoundException) { Send(new { type = "error", message = "未检测到 Microsoft Edge WebView2 Evergreen Runtime。请从微软官方安装 Runtime；550W 未自动下载任何内容。" }); Close(); return; }
            web = new WebView2CompositionControl { DefaultBackgroundColor = System.Drawing.Color.Black, Visibility = Visibility.Hidden };
            grid.Children.Insert(0, web);
            var options = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = "--no-first-run --autoplay-policy=no-user-gesture-required" };
            var environment = await CoreWebView2Environment.CreateAsync(browserFolder, System.IO.Path.Combine(session.DataDirectory, "WebView2", session.SessionId), options);
            if (life.IsCancellationRequested) return;
            await web.EnsureCoreWebView2Async(environment);
            if (life.IsCancellationRequested) return;
            var browser = web.CoreWebView2;
            browser.Settings.AreDefaultContextMenusEnabled = false; browser.Settings.AreDevToolsEnabled = false;
            browser.Settings.AreDefaultScriptDialogsEnabled = false; browser.Settings.IsStatusBarEnabled = false;
            browser.Settings.IsZoomControlEnabled = false;
            browser.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith("https://550w.local/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
            browser.NewWindowRequested += (_, e) => e.Handled = true;
            browser.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            foreach (var pair in session.Audio.AudioFiles) audioPaths["sfx/" + pair.Key] = pair.Value;
            foreach (var pair in session.Audio.VoiceFiles) audioPaths["voice/" + pair.Key] = pair.Value;
            browser.AddWebResourceRequestedFilter("https://audio.550w.local/*", CoreWebView2WebResourceContext.All);
            browser.WebResourceRequested += (_, e) =>
            {
                var key = new Uri(e.Request.Uri).AbsolutePath.TrimStart('/');
                try
                {
                    if(session.Kind!=AnimationKind.Boot&&key=="core"&&File.Exists(session.CoreImagePath)&&new FileInfo(session.CoreImagePath).Length<=16*1024*1024)
                        e.Response=environment.CreateWebResourceResponse(File.OpenRead(session.CoreImagePath),200,"OK","Content-Type: image/png\r\nAccess-Control-Allow-Origin: https://550w.local");
                    else if (audioPaths.TryGetValue(key, out var path) && File.Exists(path) && new FileInfo(path).Length <= 64 * 1024 * 1024 && new[] { ".wav", ".mp3", ".ogg" }.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))
                        e.Response = environment.CreateWebResourceResponse(File.OpenRead(path), 200, "OK", "Content-Type: application/octet-stream\r\nAccess-Control-Allow-Origin: https://550w.local");
                    else e.Response = environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", "");
                }
                catch { e.Response = environment.CreateWebResourceResponse(Stream.Null, 404, "Not Found", ""); }
            };
            browser.ProcessFailed += (_, e) =>
            {
                // WebView recreates these auxiliary processes itself. Closing the
                // overlay here used to expose the target during a recoverable reset.
                if(e.ProcessFailedKind is CoreWebView2ProcessFailedKind.GpuProcessExited
                    or CoreWebView2ProcessFailedKind.UtilityProcessExited
                    or CoreWebView2ProcessFailedKind.SandboxHelperProcessExited)
                {
                    Send(new { type="WEBVIEW_RECOVERING", message=$"{e.ProcessFailedKind}; exit={e.ExitCode}" });
                    coverGuard?.Reassert();return;
                }
                Send(new { type = "error", message = $"动画渲染器异常，遮罩已关闭。{e.ProcessFailedKind}; {e.Reason}; exit={e.ExitCode}; {e.ProcessDescription}" }); Close();
            };
            browser.WebMessageReceived += Message;
            var root = System.IO.Path.Combine(AppContext.BaseDirectory, "Themes");
            browser.SetVirtualHostNameToFolderMapping("550w.local", root, CoreWebView2HostResourceAccessKind.DenyCors);
            browser.NavigationCompleted += (_, e) => { if (!e.IsSuccess) Close(); };
            browser.Navigate("https://550w.local/" + session.Theme + "/" + System.IO.Path.GetFileName(page));
        }
        catch (Exception e) { Send(new { type = "error", message = "动画初始化失败：" + e.GetType().Name }); Close(); }
    }
    async void Message(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!args.Source.StartsWith("https://550w.local/", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            using var doc = JsonDocument.Parse(args.WebMessageAsJson); var type = doc.RootElement.GetProperty("type").GetString();
            if (type == "PAGE_READY")
            {
                pageReady = true; web!.Visibility = Visibility.Visible;
                if(session.ParticleStartTimestamp>0){session.ParticleElapsedMs=Stopwatch.GetElapsedTime(session.ParticleStartTimestamp).TotalMilliseconds;session.ParticleClockSentUnixMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();}
                web.CoreWebView2.PostWebMessageAsJson(WireJson.Serialize(new { type = "init", session }));
                foreach (var message in buffered) web.CoreWebView2.PostWebMessageAsJson(message); buffered.Clear();
            }
            else if (type == "VISUAL_READY")
            {
                core.Visibility=Visibility.Collapsed;
                if(!session.Preload){Opacity=1;coverGuard?.Reassert();await CommitVisual();}
                if(life.IsCancellationRequested)return;
                Send(new{type});
                web!.CoreWebView2.PostWebMessageAsJson("{\"type\":\"visual-committed\"}");
            }
            else if (type == "HANDOFF_BEGIN")
            {
                // Composition WebView supplies the real scene's alpha; the native backdrop must not mask the target.
                core.Visibility = Visibility.Collapsed; Background = Brushes.Transparent;web!.DefaultBackgroundColor=System.Drawing.Color.Transparent;
                Send(new { type });
            }
            else if (type == "REQUEST_FADE")
            {
                if (doc.RootElement.TryGetProperty("handoffComplete", out var complete) && complete.ValueKind == JsonValueKind.True)
                { if (!finishing) { finishing = true; Send(new { type = "ANIMATION_FINISHED" }); Close(); } }
                else Finish();
            }
            else if (type == "VOICE_REQUEST")
            {
                var id = doc.RootElement.GetProperty("id").GetString(); var key = doc.RootElement.GetProperty("key").GetString() ?? "";
                var permitted = session.Kind == AnimationKind.Boot ? session.Audio.BootVoice : session.Audio.ShutdownVoice;
                string? url=null;
                try { if (permitted && session.Audio.Phrases.TryGetValue(key, out var phrase)){
                    var text=phrase.Replace("{app}",session.DisplayName).Replace("{system}",session.Settings.SystemLabel);
                    if(session.Audio.VoiceName.Length>0)await voice.SpeakAsync(text,key=="READY"||session.AudioTest=="voice",life.Token);
                    if(session.Audio.VoiceName.Length==0||!voice.SystemAvailable){var file=await voice.RenderOfflineAsync(text,System.IO.Path.Combine(session.DataDirectory,"WebView2",session.SessionId,"voice"),life.Token);if(file!=null){var resource="tts/"+System.IO.Path.GetFileName(file);audioPaths[resource]=file;url="https://audio.550w.local/"+resource;}else if(session.Audio.VoiceName.Length==0)await voice.SpeakAsync(text,key=="READY"||session.AudioTest=="voice",life.Token);}
                }} catch { /* Voice failures are silent and never end the animation. */ }
                try { if (!life.IsCancellationRequested) web?.CoreWebView2.PostWebMessageAsJson(WireJson.Serialize(new { type = "VOICE_DONE", id, url })); } catch { }
            }
            else if (type == "FATAL") { Send(new { type = "error", message = "动画脚本异常，已安全关闭遮罩。" }); Close(); }
            else { Send(new { type }); if (type is "BOOT_INTRO_FINISHED" or "WAITING_LOOP_READY" or "TARGET_READY" or "HUD_OFFLINE") _ = CaptureOwnFrame(type); }
        }
        catch { Close(); }
    }
    async Task CommitVisual()
    {
        if(visualCommitted)return;visualCommitted=true;
        // Keep the native opaque backdrop alive while WPF submits the first WebView frame.
        var rendered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int frames=0;EventHandler? handler=null;
        handler=(_,_)=>{if(++frames>=2)rendered.TrySetResult();};CompositionTarget.Rendering+=handler;
        try{await rendered.Task.WaitAsync(TimeSpan.FromMilliseconds(800),life.Token);}
        catch(TimeoutException){ /* Backdrop remains opaque even if rendering was delayed. */ }
        finally{CompositionTarget.Rendering-=handler;}
        coverGuard?.Reassert();
    }
    void Finish()
    {
        if (finishing) return; finishing = true;
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(session.Settings.Scale(session.Settings.FadeMs))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        fade.Completed += (_, _) => { Send(new { type = "ANIMATION_FINISHED" }); Close(); }; BeginAnimation(OpacityProperty, fade);
    }
    async Task CaptureOwnFrame(string phase)
    {
        // Optional development evidence: captures only our own local WebView, never a target client's window.
        var directory = Environment.GetEnvironmentVariable("550W_CAPTURE_DIR"); if (string.IsNullOrWhiteSpace(directory)) return;
        try
        {
            await Task.Delay(phase == "TARGET_READY" ? Math.Max(60, session.Settings.Scale(450)) : 60, life.Token); if (life.IsCancellationRequested || web == null) return;
            var result = await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", "{\"format\":\"png\"}");
            using var data = JsonDocument.Parse(result); Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(System.IO.Path.Combine(directory, session.SessionId + "-" + phase + ".png"), Convert.FromBase64String(data.RootElement.GetProperty("data").GetString()!), life.Token);
        }
        catch { }
    }
}
