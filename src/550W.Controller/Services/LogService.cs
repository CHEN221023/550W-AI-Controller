using System;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace Controller550W.Controller.Services;

public sealed class LogService
{
    static readonly object Sync = new(); readonly string directory;
    public static DateTimeOffset SessionStartedUtc { get; } = DateTimeOffset.UtcNow;
    public LogService(string dataDirectory)
    {
        directory = Path.Combine(dataDirectory, "Logs");
        try { Directory.CreateDirectory(directory); Prune(); } catch { }
    }
    public void Write(string eventName, string detail = "") => Write(InferLevel(eventName), eventName, detail);
    public void Warning(string eventName, string detail = "") => Write("WARN", eventName, detail);
    public void Error(string eventName, Exception exception, bool crash = false)
    {
        // Exception messages can contain user paths or launch arguments. Never persist them here.
        var methods = (new StackTrace(exception, false).GetFrames() ?? []).Select(f => f.GetMethod())
            .Where(m => m?.DeclaringType?.Namespace?.StartsWith("Controller550W", StringComparison.Ordinal) == true)
            .Take(16).Select(m => $"{m!.DeclaringType!.Name}.{m.Name}");
        var detail = $"exception={exception.GetType().Name}, hresult={exception.HResult}, methods={string.Join(" > ", methods)}";
        Write("ERROR", eventName, detail);
        if (!crash) return;
        lock (Sync) try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "crash.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Move(path, Path.Combine(directory, "crash-previous.log"), true);
            File.AppendAllText(path, Format("ERROR", eventName, detail));
        }
        catch { }
    }
    void Write(string level, string eventName, string detail)
    {
        lock (Sync) try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"controller-{DateTime.Today:yyyyMMdd}.log");
            if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024)
                File.Move(path, Path.Combine(directory, $"controller-{DateTime.Now:yyyyMMdd-HHmmssfff}.log"), true);
            File.AppendAllText(path, Format(level, eventName, detail));
            Prune();
        }
        catch { /* Logging must never affect a monitored client. */ }
    }
    static string Format(string level, string eventName, string detail) => $"{DateTimeOffset.Now:O} [{Clean(eventName, 80)}] level={level}, {Clean(detail, 2000)}{Environment.NewLine}";
    static string Clean(string value, int max) { value = (value ?? "").Replace('\r', ' ').Replace('\n', ' '); return value[..Math.Min(value.Length, max)]; }
    static string InferLevel(string name) => name.Contains("Error", StringComparison.OrdinalIgnoreCase) || name.Contains("Failed", StringComparison.OrdinalIgnoreCase) || name.Contains("Crash", StringComparison.OrdinalIgnoreCase)
        ? "ERROR" : name.Contains("Unavailable", StringComparison.OrdinalIgnoreCase) || name.Contains("Fallback", StringComparison.OrdinalIgnoreCase) || name.Contains("WATCHDOG", StringComparison.Ordinal) ? "WARN" : "INFO";
    void Prune()
    {
        foreach (var f in new DirectoryInfo(directory).GetFiles("controller-*.log").OrderByDescending(x => x.LastWriteTimeUtc).Skip(8))
            try { f.Delete(); } catch { }
    }
}
