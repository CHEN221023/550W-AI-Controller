namespace Controller550W.Core;

public sealed record AppSnapshot(int ProcessCount, int VisibleWindowCount, long PrimaryWindow = 0);
public sealed record LifecycleResult(RuntimePhase Phase, AnimationKind? Animation, bool Stopped = false);

/// <summary>Pure state reducer. Minimized windows must be counted as present by the platform probe.</summary>
public sealed class AppLifecycle
{
    AppSnapshot previous = new(0, 0);
    bool initialized;
    DateTimeOffset lastBoot = DateTimeOffset.MinValue, lastShutdown = DateTimeOffset.MinValue;
    DateTimeOffset expectedUntil = DateTimeOffset.MinValue;
    bool managedWindowObserved, processBootAwaitingWindow;
    public string LaunchSessionId { get; private set; } = "";
    public bool ExpectedLaunch(DateTimeOffset now) => now < expectedUntil;
    public void BeginManagedLaunch(string sessionId, DateTimeOffset now)
    {
        LaunchSessionId = sessionId; expectedUntil = now.AddSeconds(30); managedWindowObserved = false;
        lastBoot = now; initialized = true; Phase = RuntimePhase.Starting;
    }
    public void MarkManagedReady() { if (managedWindowObserved) expectedUntil = DateTimeOffset.MinValue; }
    public bool TryBeginProcessBoot(AppProfile profile,int processCount,DateTimeOffset now)
    {
        if(!initialized||previous.ProcessCount>0||processCount==0||!profile.Enabled||ExpectedLaunch(now)||(now-lastBoot).TotalMilliseconds<profile.CooldownMs)return false;
        lastBoot=now;previous=new(processCount,0);Phase=RuntimePhase.Starting;processBootAwaitingWindow=true;return true;
    }
    public RuntimePhase Phase { get; private set; } = RuntimePhase.NotRunning;
    public LifecycleResult Update(AppProfile profile, AppSnapshot snapshot, DateTimeOffset now, bool baseline = false)
    {
        var present = snapshot.ProcessCount > 0;
        var visible = snapshot.VisibleWindowCount > 0;
        var expected = ExpectedLaunch(now);
        // A window can briefly lose WS_VISIBLE while minimizing or rebuilding during startup.
        var graceHold = !visible && present && previous.VisibleWindowCount > 0 && (now - lastBoot).TotalMilliseconds < profile.StartupGraceMs;
        if (graceHold) snapshot = snapshot with { VisibleWindowCount = previous.VisibleWindowCount, PrimaryWindow = previous.PrimaryWindow };
        visible |= graceHold;
        AnimationKind? action = null; bool stopObserved = false;
        if (initialized && !baseline && profile.Enabled)
        {
            var started = profile.StartTrigger == StartTrigger.ProcessStart
                ? previous.ProcessCount == 0 && present
                : previous.VisibleWindowCount == 0 && visible;
            if(processBootAwaitingWindow&&visible){started=false;processBootAwaitingWindow=false;}
            if(!present)processBootAwaitingWindow=false;
            var stopped = profile.StopTrigger == StopTrigger.ProcessExit
                ? previous.ProcessCount > 0 && !present
                : previous.VisibleWindowCount > 0 && !visible;
            // A booting process that crashes before creating a window also ends its overlay.
            stopped |= Phase == RuntimePhase.Starting && previous.ProcessCount > 0 && !present;
            if (started && !expected && (now - lastBoot).TotalMilliseconds >= profile.CooldownMs)
            { action = AnimationKind.Boot; lastBoot = now; }
            else if (stopped && (now - lastShutdown).TotalMilliseconds >= profile.CooldownMs)
            { stopObserved = true; lastShutdown = now; }
        }
        Phase = visible ? RuntimePhase.Visible : present
            ? (action == AnimationKind.Boot || (Phase == RuntimePhase.Starting && previous.VisibleWindowCount == 0) ? RuntimePhase.Starting : RuntimePhase.Hidden)
            : initialized ? RuntimePhase.Stopped : RuntimePhase.NotRunning;
        previous = snapshot; initialized = true;
        if (expected && snapshot.VisibleWindowCount > 0) managedWindowObserved = true;
        return new(Phase, action, stopObserved);
    }
}

public sealed class ReadinessGate
{
    long handle;
    DateTimeOffset? stableSince;
    public bool Update(long windowHandle, bool acceptable, DateTimeOffset now, int delayMs)
    {
        if (!acceptable || windowHandle == 0) { stableSince = null; handle = 0; return false; }
        if (handle != windowHandle || stableSince == null) { handle = windowHandle; stableSince = now; }
        return (now - stableSince.Value).TotalMilliseconds >= delayMs;
    }
}

public sealed record RouteSummary(string Status, int Successes, int Attempts, double? MedianMs, double? JitterMs)
{
    public static RouteSummary FromSamples(IReadOnlyList<double?> samples)
    {
        var ok = samples.Where(x => x.HasValue).Select(x => x!.Value).Order().ToArray();
        if (ok.Length == 0) return new("offline", 0, samples.Count, null, null);
        var median = ok.Length % 2 == 0 ? (ok[ok.Length / 2 - 1] + ok[ok.Length / 2]) / 2 : ok[ok.Length / 2];
        var jitter = ok.Length >= 2 ? ok[^1] - ok[0] : (double?)null;
        var status = ok.Length < samples.Count || samples.Count < 3 ? "degraded"
            : jitter > 150 || median > 1200 ? "unstable" : "stable";
        return new(status, ok.Length, samples.Count, median, jitter);
    }
    public string Display => MedianMs is null ? Status.ToUpperInvariant() : $"{Status.ToUpperInvariant()} · {MedianMs:0} ms · {Successes}/{Attempts}";
}
