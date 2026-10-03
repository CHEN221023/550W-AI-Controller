using System.Diagnostics;
using System.Text;

namespace Controller550W.Controller.Native;

public sealed record ProcessIdentity(int Pid, string Name, string Path);
public sealed record ObservedWindow(nint Handle, int Pid, string Title, bool Visible, bool Minimized);

public static class WindowProbe
{
    public static List<ObservedWindow> Enumerate(ISet<int>? selected = null)
    {
        var result = new List<ObservedWindow>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            try
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
                // GetWindowText for a same-process WPF window is synchronous. Never make a worker
                // wait for our dispatcher during cancellation; target-only probes also avoid unrelated UIs.
                if(pid==Environment.ProcessId||(selected!=null&&!selected.Contains((int)pid)))return true;
                if (NativeMethods.GetAncestor(hwnd, 2) != hwnd || NativeMethods.GetWindow(hwnd, 4) != 0) return true;
                if (((long)NativeMethods.GetWindowLongPtrW(hwnd, -20) & 0x80) != 0) return true;
                var minimized = NativeMethods.IsIconic(hwnd);
                if (!minimized && NativeMethods.DwmGetWindowAttribute(hwnd, 14, out var cloaked, 4) == 0 && cloaked != 0) return true;
                if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return true;
                if (!minimized && (rect.Right - rect.Left < 160 || rect.Bottom - rect.Top < 100)) return true;
                var text = new StringBuilder(512); NativeMethods.GetWindowText(hwnd, text, text.Capacity);
                if (text.Length == 0) return true;
                result.Add(new(hwnd, (int)pid, text.ToString(), NativeMethods.IsWindowVisible(hwnd), minimized));
            }
            catch { /* A window may disappear while EnumWindows is visiting it. */ }
            return true;
        }, 0);
        return result;
    }
    public static List<ObservedWindow> ForProfile(AppProfile p, IEnumerable<ProcessIdentity> processes)
    {
        var ids = processes.Where(x => p.MatchesProcess(x.Name, x.Path)).Select(x => x.Pid).ToHashSet();
        return Enumerate(ids).Where(w => p.WindowTitleMatch.Length == 0 || w.Title.Contains(p.WindowTitleMatch, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    public static bool IsResponsive(nint hwnd, bool allowMinimized)
    {
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd) || (!allowMinimized && NativeMethods.IsIconic(hwnd)) || NativeMethods.IsHungAppWindow(hwnd)) return false;
        return NativeMethods.SendMessageTimeout(hwnd, 0, 0, 0, 0x2 | 0x20, 90, out _) != 0;
    }
    public static ProcessIdentity? ReadProcess(int pid, string? knownName = null)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            string path; try { path = p.MainModule?.FileName ?? ""; } catch { path = ""; }
            return new(pid, knownName ?? p.ProcessName, path);
        }
        catch { return null; }
    }
    public static List<ProcessIdentity> Processes(bool readAllPaths = false, IReadOnlyList<AppProfile>? profiles = null)
    {
        var list = new List<ProcessIdentity>();
        foreach (var p in Process.GetProcesses())
        {
            using (p) try
            {
                var name = p.ProcessName; var path = "";
                if (readAllPaths || profiles?.Any(x => x.MatchExecutablePath && x.HasProcessName(name)) == true)
                    try { path = p.MainModule?.FileName ?? ""; } catch { }
                list.Add(new(p.Id, name, path));
            }
            catch { }
        }
        return list;
    }
}
