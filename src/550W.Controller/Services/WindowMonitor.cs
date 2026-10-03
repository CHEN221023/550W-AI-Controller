using Controller550W.Controller.Native;

namespace Controller550W.Controller.Services;

public sealed class WindowMonitor : IDisposable
{
    readonly List<nint> hooks = []; readonly NativeMethods.WinEventProc callback;
    public event Action<nint, uint>? Changed;
    public WindowMonitor() => callback = OnEvent;
    public void Start(LogService log)
    {
        // OUTOFCONTEXT + SKIPOWNPROCESS: callback is in our process, no DLL injection.
        foreach (var range in new[] { (0x8001u, 0x8003u), (0x800Bu,0x800Bu), (3u, 3u), (0x16u, 0x17u) })
        {
            var hook = NativeMethods.SetWinEventHook(range.Item1, range.Item2, 0, callback, 0, 0, 2);
            if (hook != 0) hooks.Add(hook); else log.Write("WindowEventUnavailable");
        }
    }
    void OnEvent(nint hook, uint type, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        if (hwnd == 0 || objectId != 0 || childId != 0) return;
        try { Changed?.Invoke(hwnd, type); } catch { }
    }
    public void Dispose() { foreach (var h in hooks) NativeMethods.UnhookWinEvent(h); hooks.Clear(); }
}
