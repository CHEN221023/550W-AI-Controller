using System.Management;
using System.Diagnostics;

namespace Controller550W.Controller.Services;

public sealed class ProcessMonitor : IDisposable
{
    ManagementEventWatcher? start, stop;
    Timer? fallback;
    int polling, disposed;
    Dictionary<string, HashSet<int>> previous = new(StringComparer.OrdinalIgnoreCase);
    public event Action<int, string, bool>? Changed;
    public bool Available { get; private set; }
    public void Start(LogService log, Func<IEnumerable<string>> processNames)
    {
        try
        {
            start = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            stop = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            start.EventArrived += (_, e) => Receive(e, true);
            stop.EventArrived += (_, e) => Receive(e, false);
            start.Start(); stop.Start(); Available = true;
        }
        catch (Exception e)
        {
            log.Write("ProcessEventsUnavailable", e.GetType().Name);
            StopEvents();
            Poll(processNames); // Seed existing processes without replaying a launch.
            fallback = new Timer(_ => Poll(processNames), null, 200, 200);
            log.Write("ProcessPollingFallback", "configured process names only, interval=200ms");
        }
    }
    void Poll(Func<IEnumerable<string>> processNames)
    {
        if (Volatile.Read(ref disposed) != 0 || Interlocked.Exchange(ref polling, 1) != 0) return;
        try
        {
            var next = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in processNames().Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
            {
                var ids = new HashSet<int>();
                try { foreach (var process in Process.GetProcessesByName(name)) using (process) ids.Add(process.Id); }
                catch { if (previous.TryGetValue(name, out var retained)) next[name] = retained; continue; }
                next[name] = ids;
                if (!previous.TryGetValue(name, out var old)) continue;
                foreach (var pid in ids.Except(old)) Notify(pid, name, true);
                foreach (var pid in old.Except(ids)) Notify(pid, name, false);
            }
            previous = next;
        }
        catch { /* Reconciliation remains available if configuration changes during enumeration. */ }
        finally { Volatile.Write(ref polling, 0); }
    }
    void Notify(int pid, string name, bool started)
    { if (Volatile.Read(ref disposed) == 0) try { Changed?.Invoke(pid, name, started); } catch { } }
    void Receive(EventArrivedEventArgs e, bool started)
    {
        try { Changed?.Invoke(Convert.ToInt32(e.NewEvent["ProcessID"]), Convert.ToString(e.NewEvent["ProcessName"]) ?? "", started); } catch { }
    }
    void StopEvents()
    {
        foreach (var w in new[] { start, stop }) if (w != null) { try { w.Stop(); } catch { } w.Dispose(); }
        start = stop = null; Available = false;
    }
    public void Dispose() { Interlocked.Exchange(ref disposed, 1); fallback?.Dispose(); StopEvents(); }
}
