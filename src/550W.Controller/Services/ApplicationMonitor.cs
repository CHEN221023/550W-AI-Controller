using System.Collections.Concurrent;
using System.Diagnostics;
using Controller550W.Controller.Native;

namespace Controller550W.Controller.Services;

public sealed class ApplicationMonitor : IDisposable
{
    readonly LogService log; readonly ProcessMonitor processes = new(); readonly WindowMonitor windows = new();
    readonly ConcurrentDictionary<int, ProcessIdentity> known = new();
    readonly ConcurrentDictionary<nint, string> trackedWindows = new();
    readonly ConcurrentDictionary<string, byte> pending = new();
    readonly ConcurrentDictionary<string, AppLifecycle> states = new();
    readonly SemaphoreSlim refreshLock = new(1, 1); readonly CancellationTokenSource lifetime = new();
    ControllerSettings config; Timer? fallback;
    public event Action<AppProfile, AnimationKind, nint,long>? AnimationRequested;
    public event Action<AppProfile>? TargetStopped;
    public event Action<nint,uint>? WindowMetadataChanged;
    public ApplicationMonitor(ControllerSettings config, LogService log) { this.config = config; this.log = log; }
    public void Start()
    {
        foreach (var p in WindowProbe.Processes(profiles: config.Profiles)) known[p.Pid] = p;
        // Initial Refresh has no asynchronous wait: establish the existing-app baseline first.
        RefreshAll(true).GetAwaiter().GetResult();
        processes.Changed += ProcessChanged; windows.Changed += WindowChanged;
        windows.Start(log); processes.Start(log,()=>config.Profiles.Where(p=>p.Enabled).SelectMany(p=>p.ProcessNames).Select(AppProfile.NormalizeProcessName).Distinct(StringComparer.OrdinalIgnoreCase));
        fallback = new Timer(_ => _ = Reconcile(), null, TimeSpan.FromSeconds(config.FallbackPollingSeconds), TimeSpan.FromSeconds(config.FallbackPollingSeconds));
        log.Write("WatcherStart", $"processEvents={processes.Available}, fallback={config.FallbackPollingSeconds}s");
    }
    public void Configure(ControllerSettings settings)
    {
        config = settings; states.Clear(); trackedWindows.Clear();
        fallback?.Change(TimeSpan.FromSeconds(config.FallbackPollingSeconds), TimeSpan.FromSeconds(config.FallbackPollingSeconds));
        _ = Reconcile(true);
    }
    void ProcessChanged(int pid, string name, bool started)
    {
        var origin=Stopwatch.GetTimestamp();
        var profiles = config.Profiles.Where(p => p.HasProcessName(name)).ToArray();
        if (profiles.Length == 0) return;
        if (started)
        {
            // Path lookup is outside the WMI callback and window-event thread.
            _ = Task.Run(async () =>
            {
                try {
                var identity = profiles.Any(p=>p.MatchExecutablePath)?WindowProbe.ReadProcess(pid, name):new ProcessIdentity(pid,name,"");
                if (identity != null) known[pid] = identity;
                foreach (var p in profiles){await EarlyProcessBoot(p,origin);Schedule(p,0,origin);}
                } catch(OperationCanceledException) { } catch(Exception e) { log.Write("MonitorError",e.GetType().Name); }
            });
        }
        else { known.TryRemove(pid, out _); foreach (var p in profiles) Schedule(p, 160); }
    }
    async Task EarlyProcessBoot(AppProfile p,long origin)
    {
        if(config.Paused||p.WindowTitleMatch.Length>0)return;
        await refreshLock.WaitAsync(lifetime.Token);
        try{var count=known.Values.Count(x=>p.MatchesProcess(x.Name,x.Path));var state=states.GetOrAdd(p.Id,_=>new());if(state.TryBeginProcessBoot(p,count,DateTimeOffset.UtcNow)){log.Write("BootTrigger",$"{p.DisplayName}, process-first");AnimationRequested?.Invoke(WireJson.Clone(p),AnimationKind.Boot,0,origin);}}
        finally{refreshLock.Release();}
    }
    void WindowChanged(nint hwnd, uint type)
    {
        WindowMetadataChanged?.Invoke(hwnd,type);
        if (trackedWindows.TryGetValue(hwnd, out var id))
        {
            var profile = config.Profiles.FirstOrDefault(x => x.Id == id);
            if (profile != null) Schedule(profile, type is 0x8001 or 0x8003 or 0x16 or 0x17 ? 300 : 0);
            return;
        }
        if (type is 0x8001 or 0x8003) return;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (known.TryGetValue((int)pid, out var identity))
        {
            foreach (var p in config.Profiles.Where(p => p.MatchesProcess(identity.Name, identity.Path))) Schedule(p, 0);
        }
        else _ = Task.Run(() =>
        {
            var p = WindowProbe.ReadProcess((int)pid);
            if (p == null) return;
            var matches = config.Profiles.Where(x => x.MatchesProcess(p.Name, p.Path)).ToArray();
            if (matches.Length == 0) return;
            known[p.Pid] = p; foreach (var profile in matches) Schedule(profile, 0);
        });
    }
    void Schedule(AppProfile p, int delay,long origin=0)
    {
        if(origin==0)origin=Stopwatch.GetTimestamp();
        if (!pending.TryAdd(p.Id, 0)) return;
        _ = Task.Run(async () =>
        {
            try { if (delay > 0) await Task.Delay(delay, lifetime.Token); await Refresh(p, false,origin); }
            catch (OperationCanceledException) { }
            catch (Exception e) { log.Write("MonitorError", e.GetType().Name); }
            finally { pending.TryRemove(p.Id, out _); }
        });
    }
    async Task Reconcile(bool baseline = false)
    {
        if (lifetime.IsCancellationRequested) return;
        try
        {
            var all = WindowProbe.Processes(profiles: config.Profiles);
            var ids = all.Select(x => x.Pid).ToHashSet();
            foreach (var id in known.Keys.Where(x => !ids.Contains(x))) known.TryRemove(id, out _);
            foreach (var p in all) known[p.Pid] = p;
            await RefreshAll(baseline);
        }
        catch (Exception e) { log.Write("ReconcileError", e.GetType().Name); }
    }
    async Task RefreshAll(bool baseline) { foreach (var p in config.Profiles.ToArray()) await Refresh(p, baseline); }
    async Task Refresh(AppProfile p, bool baseline,long origin=0)
    {
        if (lifetime.IsCancellationRequested) return;
        await refreshLock.WaitAsync(lifetime.Token);
        try
        {
            var identities = known.Values.Where(x => p.MatchesProcess(x.Name, x.Path)).ToArray();
            var wnd = WindowProbe.ForProfile(p, identities);
            var state = states.GetOrAdd(p.Id, _ => new());
            if (!baseline && wnd.All(w => !w.Visible && !w.Minimized) && state.Phase == RuntimePhase.Visible)
            {
                await Task.Delay(300, lifetime.Token);
                wnd = WindowProbe.ForProfile(p, identities);
            }
            foreach (var old in trackedWindows.Where(x => x.Value == p.Id).Select(x => x.Key).ToArray()) trackedWindows.TryRemove(old, out _);
            foreach (var w in wnd) trackedWindows[w.Handle] = p.Id;
            var present = wnd.Where(w => w.Visible || w.Minimized).ToArray();
            var primary = present.FirstOrDefault(w => !w.Minimized) ?? present.FirstOrDefault();
            if (primary != null) p.LastMonitorDevice = System.Windows.Forms.Screen.FromHandle(primary.Handle).DeviceName;
            var result = state.Update(p, new(identities.Length, present.Length, (long)(primary?.Handle ?? 0)), DateTimeOffset.UtcNow, baseline);
            p.RuntimeStatus = state.ExpectedLaunch(DateTimeOffset.UtcNow) ? "STARTING_MANAGED" : result.Phase switch { RuntimePhase.Visible => "窗口已显示", RuntimePhase.Starting => "正在启动", RuntimePhase.Hidden => "后台 / 托盘", _ => "未运行" };
            // Stop observations remain lifecycle evidence. They NEVER start a Shutdown overlay.
            if (result.Stopped)
            { log.Write("TargetStopped", $"{p.DisplayName}, pids={identities.Length}, windows={present.Length}"); TargetStopped?.Invoke(p); }
            if (!config.Paused && result.Animation == AnimationKind.Boot)
            {
                log.Write(result.Animation == AnimationKind.Boot ? "BootTrigger" : "ShutdownTrigger", $"{p.DisplayName}, pids={identities.Length}, windows={present.Length}");
                var snapshot = WireJson.Clone(p); snapshot.LastMonitorDevice = p.LastMonitorDevice;
                AnimationRequested?.Invoke(snapshot, result.Animation.Value, primary?.Handle ?? 0,origin==0?Stopwatch.GetTimestamp():origin);
            }
        }
        finally { refreshLock.Release(); }
    }
    public TargetStatus GetTarget(AppProfile p, ReadinessGate gate)
    {
        var identities = known.Values.Where(x => p.MatchesProcess(x.Name, x.Path)).Where(x=>Alive(x.Pid)).ToArray();
        var present=WindowProbe.ForProfile(p,identities).Where(x=>x.Visible||x.Minimized).ToArray();
        var wnd = present.Where(x => x.Visible && (p.AllowMinimizedReady || !x.Minimized)).ToArray();
        var w = wnd.FirstOrDefault(x => WindowProbe.IsResponsive(x.Handle, p.AllowMinimizedReady));
        var ready = gate.Update((long)(w?.Handle ?? 0), identities.Length > 0 && w != null, DateTimeOffset.UtcNow, p.ReadyDelayMs);
        if (ready && states.TryGetValue(p.Id, out var state)) state.MarkManagedReady();
        return new(p.DisplayName, identities.Length > 0, present.Length > 0, ready, ready ? "ready" : identities.Length > 0 ? "initializing" : "stopped", (long)(w?.Handle ?? present.FirstOrDefault()?.Handle ?? 0));
    }
    bool Alive(int pid){try{using var process=Process.GetProcessById(pid);if(!process.HasExited)return true;}catch(ArgumentException){}catch{return true;}known.TryRemove(pid,out _);return false;}
    public void BeginManagedLaunch(AppProfile p, string sessionId)
    {
        states.GetOrAdd(p.Id, _ => new()).BeginManagedLaunch(sessionId, DateTimeOffset.UtcNow);
        p.RuntimeStatus = "STARTING_MANAGED";
        log.Write("ManagedLaunchSession", $"{p.DisplayName}, session={sessionId}");
    }
    public nint FindWindow(AppProfile p) => WindowProbe.ForProfile(p, known.Values.Where(x => p.MatchesProcess(x.Name, x.Path))).FirstOrDefault(x => x.Visible || x.Minimized)?.Handle ?? 0;
    public bool IsAnimationRelevant(AppProfile p, AnimationKind kind)
    {
        var present = known.Values.Any(x => p.MatchesProcess(x.Name, x.Path));
        if (kind == AnimationKind.Shutdown) return p.StopTrigger == StopTrigger.WindowHide
            ? WindowProbe.ForProfile(p, known.Values).All(x => !x.Visible && !x.Minimized) : !present;
        return present;
    }
    public void Dispose() { lifetime.Cancel(); fallback?.Dispose(); windows.Dispose(); processes.Dispose(); }
}
