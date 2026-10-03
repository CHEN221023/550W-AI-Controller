using System.Management;
using Controller550W.Controller.Native;

namespace Controller550W.Controller.Services;

public sealed class DiagnosticsService(LogService log)
{
    public async Task RunAsync(ControllerSettings settings, AppProfile profile, Action<DiagnosticRow> update, CancellationToken token)
    {
        var jobs = new List<Task>();
        if (settings.HardwareDiagnostics && profile.HardwareDiagnostics)
        {
            jobs.Add(Safe("cpu", "CPU CORE", () => Cpu(update, token), update));
            jobs.Add(Safe("memory", "MEMORY ARRAY", () => Task.Run(() => Memory(update), token), update));
            jobs.Add(Safe("gpu", "GRAPHICS PROCESSOR", () => Task.Run(() => Gpu(update), token), update));
        }
        else foreach (var key in new[] { "cpu", "memory", "gpu", "vram" }) update(new(key, key.ToUpperInvariant(), "DISABLED", "disabled"));
        if (settings.Network.Enabled && profile.NetworkDiagnostics)
            jobs.Add(new NetworkDiagnostics().RunAsync(settings.Network, update, token));
        else foreach (var key in new[] { "local", "dns", "domestic", "external", "vpn" }) update(new(key, key.ToUpperInvariant(), "DISABLED", "disabled"));
        await Task.WhenAll(jobs);
        log.Write("Diagnostics", "asynchronous checks completed");
    }
    async Task Safe(string key, string label, Func<Task> job, Action<DiagnosticRow> update)
    {
        try { await job().WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (TimeoutException) { update(new(key, label, "TIMEOUT", "timeout")); }
        catch (OperationCanceledException) { }
        catch (Exception e) { update(new(key, label, "UNAVAILABLE", "unavailable")); log.Write("DiagnosticUnavailable", $"{key}: {e.GetType().Name}"); }
    }
    static async Task Cpu(Action<DiagnosticRow> update, CancellationToken token)
    {
        if (!NativeMethods.GetSystemTimes(out var idle1, out var kernel1, out var user1)) { update(new("cpu", "CPU CORE", "UNKNOWN", "unknown")); return; }
        await Task.Delay(260, token);
        if (!NativeMethods.GetSystemTimes(out var idle2, out var kernel2, out var user2)) { update(new("cpu", "CPU CORE", "UNKNOWN", "unknown")); return; }
        var total = (kernel2 - kernel1) + (user2 - user1);
        var usage = total == 0 ? 0 : 100d * (total - Math.Min(total, idle2 - idle1)) / total;
        update(new("cpu", "CPU CORE", $"{usage:0}% · {Environment.ProcessorCount} LOGICAL", "online", "短时间负载采样；不代表物理健康诊断"));
    }
    static void Memory(Action<DiagnosticRow> update)
    {
        var status = new NativeMethods.MemoryStatus();
        if (!NativeMethods.GlobalMemoryStatusEx(status)) { update(new("memory", "MEMORY ARRAY", "UNKNOWN", "unknown")); return; }
        update(new("memory", "MEMORY ARRAY", $"{(status.TotalPhysical - status.AvailablePhysical) / 1073741824d:0.0} / {status.TotalPhysical / 1073741824d:0.0} GB · {status.Load}%", "online", $"可用 {status.AvailablePhysical / 1073741824d:0.0} GB"));
    }
    static void Gpu(Action<DiagnosticRow> update)
    {
        var gpu = GpuProbe.Read();
        if (gpu != null)
        {
            update(new("gpu", "GRAPHICS PROCESSOR", gpu.Value.Name, "online"));
            update(new("vram", "DEDICATED VRAM", gpu.Value.DedicatedBytes > 0 ? $"{gpu.Value.DedicatedBytes / 1073741824d:0.0} GB TOTAL" : "SHARED MEMORY", "online", "DXGI 专用显存总量；当前占用未测量")); return;
        }
        using var query = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
        query.Options.Timeout = TimeSpan.FromSeconds(2);
        using var rows = query.Get();
        var names = new List<string>(); foreach (ManagementObject row in rows) using (row) if (row["Name"] is string name) names.Add(name);
        update(new("gpu", "GRAPHICS PROCESSOR", names.Count > 0 ? string.Join(" / ", names) : "UNAVAILABLE", names.Count > 0 ? "online" : "unavailable"));
        update(new("vram", "DEDICATED VRAM", "UNKNOWN", "unknown", "WMI AdapterRAM 的位宽不足以可靠报告大显存"));
    }
}
