using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Controller550W.Controller.Native;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Controller550W.Controller.Services;

public static class DiagnosticExportService
{
    public static Task<string> ExportAsync(string destination, string dataDirectory, ControllerSettings settings, CancellationToken cancellationToken = default)
    {
        var snapshot = WireJson.Clone(settings);
        return Task.Run(() =>
        {
            var log = new LogService(dataDirectory);
            try
            {
                var result = DiagnosticArchive.Create(destination, dataDirectory, snapshot, SystemInformation(), cancellationToken);
                log.Write("DiagnosticsExported");
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { log.Error("DiagnosticsExportFailed", ex); throw; }
        }, cancellationToken);
    }

    static object SystemInformation()
    {
        var data = new Dictionary<string, object?>
        {
            ["application"] = "550W AI Controller",
            ["version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unavailable",
            ["exportedUtc"] = DateTimeOffset.UtcNow,
            ["sessionStartedUtc"] = LogService.SessionStartedUtc,
            ["windowsVersion"] = Environment.OSVersion.Version.ToString(),
            ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["dotnetVersion"] = Environment.Version.ToString(),
            ["logicalProcessors"] = Environment.ProcessorCount,
            ["webView2Version"] = WebViewVersion(),
            ["cpuModel"] = "unavailable",
            ["graphics"] = "unavailable",
            ["monitors"] = Array.Empty<object>()
        };
        try
        {
            using var cpu = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            data["cpuModel"] = SafeHardware(cpu?.GetValue("ProcessorNameString")?.ToString());
        }
        catch { }
        try
        {
            var memory = new NativeMethods.MemoryStatus();
            if (NativeMethods.GlobalMemoryStatusEx(memory)) { data["physicalMemoryBytes"] = memory.TotalPhysical; data["availableMemoryBytes"] = memory.AvailablePhysical; }
        }
        catch { }
        try
        {
            var gpu = GpuProbe.Read();
            if (gpu != null) data["graphics"] = new { model = SafeHardware(gpu.Value.Name), dedicatedMemoryBytes = gpu.Value.DedicatedBytes };
        }
        catch { }
        try
        {
            data["monitors"] = Forms.Screen.AllScreens.Select((screen, index) =>
            {
                var bounds = screen.Bounds;
                var rectangle = new Rect { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom };
                uint dpiX = 0, dpiY = 0;
                try { var monitor = MonitorFromRect(ref rectangle, 2); if (GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0) { dpiX = 0; dpiY = 0; } } catch { }
                return new { number = index + 1, primary = screen.Primary, x = bounds.X, y = bounds.Y, width = bounds.Width, height = bounds.Height, workingWidth = screen.WorkingArea.Width, workingHeight = screen.WorkingArea.Height, dpiX, dpiY, scalePercent = dpiX == 0 ? 0 : (int)Math.Round(dpiX * 100d / 96), dpiStatus = dpiX == 0 ? "unavailable" : "available" };
            }).ToArray();
        }
        catch { }
        return data;
    }
    static string SafeHardware(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160 || value.Contains('\\') || value.Contains('/') || value.Contains('@') || value.Contains('\n')) return "unavailable";
        return value.Trim();
    }
    static string WebViewVersion()
    {
        // Read only product/version metadata. Never start WebView or include installation paths.
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                try
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var clients = root.OpenSubKey(@"SOFTWARE\Microsoft\EdgeUpdate\Clients");
                    if (clients == null) continue;
                    foreach (var name in clients.GetSubKeyNames())
                    {
                        using var product = clients.OpenSubKey(name);
                        if (product?.GetValue("name")?.ToString()?.Contains("WebView2", StringComparison.OrdinalIgnoreCase) != true) continue;
                        var version = product.GetValue("pv")?.ToString() ?? "";
                        if (Regex.IsMatch(version, @"^\d{1,5}(\.\d{1,6}){1,3}$")) return version;
                    }
                }
                catch { }
        try
        {
            var core = Path.Combine(AppContext.BaseDirectory, "Microsoft.Web.WebView2.Core.dll");
            if (File.Exists(core))
            {
                var sdk = FileVersionInfo.GetVersionInfo(core).FileVersion;
                if (sdk != null && Regex.IsMatch(sdk, @"^\d+(\.\d+){1,3}$")) return "runtime unavailable; SDK " + sdk;
            }
        }
        catch { }
        return "unavailable";
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern nint MonitorFromRect(ref Rect rectangle, uint flags);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);
}
