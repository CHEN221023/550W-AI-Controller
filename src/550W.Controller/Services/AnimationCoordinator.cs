using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using Controller550W.Controller.Native;
using Controller550W.Controller.Views;
using Forms = System.Windows.Forms;

namespace Controller550W.Controller.Services;

public sealed class AnimationCoordinator : IDisposable
{
    sealed record Request(AppProfile Profile, AnimationKind Kind, nint Hwnd, bool Demo, string AudioTest, ControllerSettings? PreviewSettings, bool Managed, long Origin, bool Debug, Action? FirstVisual);
    readonly Func<ControllerSettings> settings; readonly ApplicationMonitor monitor; readonly LogService log; readonly string dataDirectory;
    readonly ConcurrentQueue<string> order = new(); readonly ConcurrentDictionary<string, Request> queued = new();
    readonly object sync = new(); readonly CancellationTokenSource lifetime = new();
    CancellationTokenSource? active; string? activeId; bool pumping;
    public event Action<string>? Notice;
    public AnimationCoordinator(Func<ControllerSettings> settings, ApplicationMonitor monitor, LogService log, string dataDirectory)
    { this.settings = settings; this.monitor = monitor; this.log = log; this.dataDirectory = dataDirectory; }
    public void RequestAnimation(AppProfile profile, AnimationKind kind, nint hwnd = 0, bool demo = false, string audioTest = "", ControllerSettings? previewSettings = null, bool managed = false, long origin = 0, bool debug = false, Action? firstVisual = null)
    {
        if (lifetime.IsCancellationRequested || (!demo && kind == AnimationKind.Shutdown)) return;
        var request = new Request(profile, kind, hwnd, demo, audioTest, previewSettings == null ? null : WireJson.Clone(previewSettings), managed, origin == 0 ? Stopwatch.GetTimestamp() : origin, debug, firstVisual); var id = demo ? "demo" : profile.Id;
        lock (sync) { if (activeId == id) active?.Cancel(); if (queued.TryAdd(id, request)) order.Enqueue(id); else queued[id] = request; if (!pumping) { pumping = true; _ = Task.Run(Pump); } }
    }
    public void CancelProfile(string id) { lock (sync) { queued.TryRemove(id, out _); if (activeId == id) active?.Cancel(); } }
    async Task Pump()
    {
        while (true)
        {
            Request? request = null; CancellationTokenSource? sessionLife = null;
            lock (sync) { while (order.TryDequeue(out var id)) if (queued.TryRemove(id, out request)) { activeId = id; active = sessionLife = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); break; } if (request == null) { pumping = false; activeId = null; active = null; return; } }
            try { if (request.Demo || request.Managed || monitor.IsAnimationRelevant(request.Profile, request.Kind)) await Play(request, sessionLife!.Token); }
            catch (OperationCanceledException) { }
            catch (Exception e) { log.Write("AnimationError", e.GetType().Name + ": " + e.Message); Notice?.Invoke("动画无法播放，目标应用可继续使用。请检查 WebView2 Runtime 或查看日志。"); }
            finally { lock (sync) { if (active == sessionLife) { active = null; activeId = null; } } sessionLife?.Dispose(); }
        }
    }
    internal System.Drawing.Rectangle ScreenFor(AnimationSettings config, nint hwnd)
    {
        Forms.Screen screen;
        if (config.Monitor == MonitorTarget.Primary) screen = Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        else if (config.Monitor == MonitorTarget.Specified) screen = Forms.Screen.AllScreens.FirstOrDefault(x => x.DeviceName == config.MonitorDevice) ?? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        else if (config.Monitor == MonitorTarget.TargetWindow && hwnd != 0 && NativeMethods.IsWindow(hwnd)) screen = Forms.Screen.FromHandle(hwnd);
        else { NativeMethods.GetCursorPos(out var point); screen = Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y)); }
        return screen.Bounds;
    }
    internal AnimationSession CreateSession(AppProfile p, AnimationKind kind, nint hwnd, bool demo = false, ControllerSettings? preview = null)
    {
        var config = WireJson.Clone(preview ?? settings()); config.Validate();
        config.Animation = ProfileSettingsResolver.ResolveAnimation(config, p);
        config.Audio = ProfileSettingsResolver.ResolveAudio(config, p);
        if (config.Animation.BootWatchdogMs >= 20000)
        {
            var a = config.Animation;
            var introduction = a.ParticleToLogoMs + a.LogoHoldMs + a.LogoBurstMs + a.MultiWindowRevealMs;
            a.BootWatchdogMs = Math.Max(a.BootWatchdogMs, a.Scale(introduction + 12000) + 8000);
        }
        var screen = ScreenFor(config.Animation, hwnd);
        return new() { Kind = kind, DisplayName = p.DisplayName, Theme = kind == AnimationKind.Boot ? p.BootTheme : p.ShutdownTheme, Settings = config.Animation, MinimumBootTimeMs = config.Animation.MinimumBootMs, Demo = demo, MonitorX = screen.X, MonitorY = screen.Y, MonitorWidth = screen.Width, MonitorHeight = screen.Height, DataDirectory = dataDirectory, Audio = config.Audio, CoreImagePath = kind == AnimationKind.Boot ? "" : File.Exists(p.CoreCachePath) ? p.CoreCachePath : Path.Combine(AppContext.BaseDirectory, "Themes", "550W", "assets", "core.png") };
    }
    async Task Play(Request request, CancellationToken token)
    {
        var config = WireJson.Clone(request.PreviewSettings ?? settings()); var session = CreateSession(request.Profile, request.Kind, request.Hwnd, request.Demo, request.PreviewSettings);
        session.DebugMode = request.Debug; session.AudioTest = request.AudioTest;
        if (request.AudioTest.Length > 0) { session.Audio.Muted = false; session.Audio.SoundEnabled = request.AudioTest == "sfx"; session.Audio.VoiceEnabled = request.AudioTest == "voice";session.Audio.BootVoice=true;session.Audio.ShutdownVoice=true; }
        var elapsed = Stopwatch.StartNew(); FirstFrameWindow? shell = null;
        var traceBoot=request.Kind==AnimationKind.Boot&&!request.Debug&&request.AudioTest.Length==0;
        if(traceBoot)log.Write("START_T0",$"session={session.SessionId}, mode={(request.Managed?"managed":"passive")}, tick={request.Origin}");
        using var hardLimit = CancellationTokenSource.CreateLinkedTokenSource(token);
        hardLimit.CancelAfter(request.Kind == AnimationKind.Boot ? session.Settings.BootWatchdogMs + 1500 : session.Settings.ShutdownWatchdogMs + 6000);
        await Application.Current.Dispatcher.InvokeAsync(() => { shell = new(new(session.MonitorX, session.MonitorY, session.MonitorWidth, session.MonitorHeight), session.CoreImagePath, request.Kind == AnimationKind.Shutdown, session.Settings.AllowEscape, () => hardLimit.Cancel(), traceBoot?session.Settings:null); shell.Show(); });
        try
        {
            await shell!.Rendered.WaitAsync(TimeSpan.FromSeconds(1), hardLimit.Token);
            session.ParticleStartTimestamp=shell.ParticleStartTimestamp;
            var composited=false;
            try{composited=await Task.Run(NativeMethods.DwmFlush,hardLimit.Token).WaitAsync(TimeSpan.FromMilliseconds(400),hardLimit.Token)==0;}catch(TimeoutException){}
            log.Write("FIRST_VISUAL", $"{session.DisplayName}, mode={(request.Managed ? "optimized" : request.Demo ? "preview" : "passive")}, latency={Stopwatch.GetElapsedTime(request.Origin).TotalMilliseconds:F1}ms");
            if(traceBoot){var first=Stopwatch.GetTimestamp();var ms=Stopwatch.GetElapsedTime(request.Origin,first).TotalMilliseconds;log.Write("OVERLAY_T1",$"session={session.SessionId}, tick={first}, T0_T1={(composited?ms.ToString("F1")+"ms":"unavailable")}, compositorConfirmed={composited}");if(shell.ParticlesPainted)log.Write("PARTICLES_T2",$"session={session.SessionId}, tick={first}, T1_T2={(composited?"0.0ms":"unavailable")}, sameCompositedFrame={composited}");}
            request.FirstVisual?.Invoke();
            session.HostElapsedMs = (int)elapsed.ElapsedMilliseconds;
            await using var host = new AnimationHostSession(session, hardLimit.Token);
            nint targetHwnd = request.Hwnd; bool presentationApplied = false;
            bool shouldPresent = request.Managed || request.Profile.ApplyPresentationToPassive;
            void Present(bool handoff = false)
            { if (targetHwnd == 0 || !shouldPresent) return; WindowPresentationService.Apply(targetHwnd, presentationApplied ? WindowPresentation.Preserve : request.Profile.WindowPresentation, handoff); presentationApplied = true; log.Write("WindowPresentation", $"{session.DisplayName}, {request.Profile.WindowPresentation}, hwnd={targetHwnd}"); }
            host.Message += (type, detail) =>
            {
                if (type == "VISUAL_READY") { log.Write("WEB_READY", $"{session.DisplayName}, latency={elapsed.ElapsedMilliseconds}ms"); _ = Application.Current.Dispatcher.InvokeAsync(() => { if(request.Kind==AnimationKind.Boot){shell?.HoldBackdrop();_ = host.Send(new{type="cover"});}else{shell?.Close();shell=null;} }); }
                else if (type == "HANDOFF_BEGIN") { _ = Application.Current.Dispatcher.InvokeAsync(() => {shell?.Close();shell=null;}); }
                else if (type == "INTERFACE_HANDOFF") Present(true);
                else if (type == "WEBVIEW_RECOVERING") log.Write(type,detail);
                else if (type == "error") { log.Write("AnimationUnavailable", detail); Notice?.Invoke(detail.Length > 160 ? detail[..160] : detail); }
                else if (type is "FIRST_FRAME" or "BOOT_INTRO_FINISHED" or "WAITING_LOOP_READY" or "TARGET_READY" or "ANIMATION_FINISHED" or "USER_SKIP" or "WATCHDOG" or "CHOREOGRAPHY_COMPLETE" or "SHUTDOWN_BEGIN" or "VOICE_PLAYING" or "PARTICLE_RESIDUE" or "LOGO_ASSEMBLING" or "LOGO_HOLD" or "LOGO_BURST" or "MULTIWINDOW_REVEAL" or "MULTIWINDOW_ACTIVE" or "HANDOFF_BEGIN" or "AUDIO_UNAVAILABLE" or "VOICE_UNAVAILABLE") log.Write(type, $"{session.DisplayName}, hostElapsed={elapsed.ElapsedMilliseconds}ms");
            };
            await host.StartAsync();
            var diagnostics = request.Kind == AnimationKind.Boot ? request.Demo ? DemoDiagnostics(host.Send) : new DiagnosticsService(log).RunAsync(config, request.Profile, row => _ = host.Send(new { type = "diagnostics", row }), hardLimit.Token) : Task.CompletedTask;
            var gate = new ReadinessGate(); var lastState = "";bool processSeen=false,windowSeen=false;long? absentSince=null;
            var targetLoop = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(180));
                do
                {
                    var target = request.Demo ? new TargetStatus(session.DisplayName, true, true, elapsed.ElapsedMilliseconds >= 1400, "ready") : monitor.GetTarget(request.Profile, gate);
                    if(!request.Demo&&request.Kind==AnimationKind.Boot)
                    {
                        processSeen|=target.ProcessDetected;windowSeen|=target.WindowDetected;
                        if(target.WindowDetected)absentSince=null;else absentSince??=elapsed.ElapsedMilliseconds;
                        var lastWindowMinimized=targetHwnd!=0&&NativeMethods.IsWindow(targetHwnd)&&NativeMethods.IsIconic(targetHwnd);
                        if(processSeen&&!target.ProcessDetected||windowSeen&&!target.WindowDetected&&!lastWindowMinimized&&elapsed.ElapsedMilliseconds>request.Profile.StartupGraceMs&&elapsed.ElapsedMilliseconds-absentSince>=1200)
                        {log.Write("BootTargetGone",session.DisplayName);hardLimit.Cancel();return;}
                    }
                    if (target.WindowHandle != 0) targetHwnd = (nint)target.WindowHandle;
                    if (target.Ready && !presentationApplied && (request.Profile.PresentationTiming == PresentationTiming.AfterStable || request.Profile.PresentationTiming == PresentationTiming.Halfway && elapsed.ElapsedMilliseconds >= session.MinimumBootTimeMs / 2)) Present();
                    var state = WireJson.Serialize(target); if (state != lastState) { await host.Send(new { type = "target", target }); lastState = state; if (target.Ready) log.Write("Ready", session.DisplayName); }
                } while (host.Available && await timer.WaitForNextTickAsync(hardLimit.Token));
            }, hardLimit.Token);
            await host.Completion.WaitAsync(hardLimit.Token); hardLimit.Cancel(); try { await Task.WhenAll(targetLoop, diagnostics).WaitAsync(TimeSpan.FromMilliseconds(500)); } catch { }
            log.Write("AnimationExited", $"{session.DisplayName}, elapsed={elapsed.ElapsedMilliseconds}ms");
        }
        finally { hardLimit.Cancel(); await Application.Current.Dispatcher.InvokeAsync(() => { shell?.Close(); shell = null; }); }
    }
    static async Task DemoDiagnostics(Func<object, Task> send)
    {
        foreach (var row in new[] { new DiagnosticRow("cpu","CPU CORE","21% · DEMO","online"),new("memory","MEMORY","15 / 32 GB · DEMO","online"),new("gpu","GPU","DEMO GRAPHICS ONLINE","online"),new("vram","VRAM TOTAL","8 GB TOTAL · DEMO","online"),new("local","LOCAL NETWORK","LINK UP · DEMO","online"),new("dns","DNS","32 ms · DEMO","online"),new("domestic","DOMESTIC ROUTE","28 ms · 3/3 · DEMO","stable"),new("external","EXTERNAL ROUTE","112 ms · 3/3 · DEMO","stable"),new("vpn","VPN ADAPTER","STABLE · DEMO","stable") }) await send(new { type = "diagnostics", row });
    }
    public void Dispose() { lifetime.Cancel(); lock (sync) { active?.Cancel(); queued.Clear(); } }
}
