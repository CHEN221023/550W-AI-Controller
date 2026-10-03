using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Controller550W.Controller.Native;

namespace Controller550W.Controller.Services;

public sealed record DiscoveredApp(string DisplayName, string ProcessName, string Path, string Source)
{public LaunchTargetKind LaunchKind{get;init;}public string LaunchTarget{get;init;}="";public string IconSource{get;init;}="";public string SourceShortcut{get;init;}="";}
public sealed record ProcessChoice(int Pid, string DisplayName, string ProcessName, string WindowTitle, string Path, bool Visible)
{
    public ImageSource? Icon { get; init; }
}

public sealed class AppDiscoveryService
{
    static readonly string[] names = ["chatgpt", "deepseek", "doubao", "豆包", "claude", "chatbox", "lm studio", "lmstudio", "ollama", "bionic", "jan", "cherry studio", "cherrystudio", "anythingllm", "msty"];
    static bool Known(string value) => names.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));
    public List<ProcessChoice> CurrentProcesses()
    {
        var windows = WindowProbe.Enumerate();
        var icons = new Dictionary<string, ImageSource?>(StringComparer.OrdinalIgnoreCase);
        return WindowProbe.Processes(true).Select(p =>
        {
            var window = windows.Where(x => x.Pid == p.Pid).OrderByDescending(x => x.Visible).FirstOrDefault();
            if (!icons.TryGetValue(p.Path, out var icon)) icons[p.Path] = icon = ReadIcon(p.Path);
            return new ProcessChoice(p.Pid, p.Name, p.Name + ".exe", window?.Title ?? "", p.Path, window?.Visible ?? false) { Icon = icon };
        }).OrderByDescending(x => x.Visible).ThenBy(x => x.DisplayName).ToList();
    }
    static ImageSource? ReadIcon(string path)
    {
        try
        {
            using var icon = File.Exists(path) ? System.Drawing.Icon.ExtractAssociatedIcon(path) : (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
            if (icon == null) return null;
            using var bitmap = icon.ToBitmap(); using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); stream.Position = 0;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
        }
        catch { return null; }
    }
    public async Task<List<DiscoveredApp>> DiscoverAsync()
    {
        var results = new List<DiscoveredApp>();
        foreach (var p in WindowProbe.Processes(true).Where(x => Known(x.Name))) results.Add(new(p.Name, Path.GetFileName(p.Path).Length > 0 ? Path.GetFileName(p.Path) : p.Name + ".exe", p.Path, "正在运行"));
        ScanRegistry(results); ScanShortcuts(results); await ScanAppx(results);
        return results.GroupBy(x => x.Path.Length > 0 ? x.Path : x.ProcessName, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderBy(x=>x.LaunchKind==LaunchTargetKind.AppUserModelId?0:x.SourceShortcut.Length>0?1:2).First()).OrderBy(x => x.DisplayName).ToList();
    }
    static void ScanRegistry(List<DiscoveredApp> results)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine }) foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall == null) continue;
                foreach (var keyName in uninstall.GetSubKeyNames())
                {
                    using var key = uninstall.OpenSubKey(keyName);
                    var name = key?.GetValue("DisplayName") as string ?? "";
                    if (!Known(name)) continue;
                    var icon = key?.GetValue("DisplayIcon") as string ?? "";
                    var path = icon.Trim('"'); var suffix = path.LastIndexOf(','); if (suffix > 0) path = path[..suffix].Trim('"');
                    if (!File.Exists(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var location = key?.GetValue("InstallLocation") as string;
                        path = location != null && Directory.Exists(location) ? Directory.EnumerateFiles(location, "*.exe").FirstOrDefault(x => Known(Path.GetFileName(x)) && !x.Contains("unins", StringComparison.OrdinalIgnoreCase)) ?? "" : "";
                    }
                    if (File.Exists(path)) results.Add(new(name, Path.GetFileName(path), path, "已安装程序"));
                }
            }
            catch { }
        }
    }
    static void ScanShortcuts(List<DiscoveredApp> results)
    {
        // WScript.Shell is used only to read .lnk metadata; never invoke its Run method.
        object? shell = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell"); if (type == null) return;
            shell = Activator.CreateInstance(type); dynamic ws = shell!;
            foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories).Where(x => Known(Path.GetFileName(x))))
                {
                    object? shortcut = null;
                    try
                    {
                        shortcut = ws.CreateShortcut(file); dynamic link = shortcut;
                        string path = link.TargetPath;
                        if (File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !new[] { "chrome.exe", "msedge.exe", "firefox.exe" }.Contains(Path.GetFileName(path).ToLowerInvariant()))
                            results.Add(new(Path.GetFileNameWithoutExtension(file), Path.GetFileName(path), path, "开始菜单"){LaunchKind=LaunchTargetKind.Shortcut,LaunchTarget=file,SourceShortcut=file,IconSource=link.IconLocation});
                    }
                    catch { }
                    finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); }
                }
            }
        }
        catch { }
        finally { if (shell != null) Marshal.FinalReleaseComObject(shell); }
    }
    static async Task ScanAppx(List<DiscoveredApp> results)
    {
        try
        {
            var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive"); info.ArgumentList.Add("-Command");
            info.ArgumentList.Add("Get-AppxPackage | Select-Object Name,InstallLocation,PackageFamilyName | ConvertTo-Json -Compress");
            using var p = Process.Start(info)!;
            var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
            try { await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8)); }
            catch (TimeoutException) { p.Kill(); return; }
            var text = await output; await error;
            using var json = JsonDocument.Parse(text);
            var items = json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement.EnumerateArray().ToArray() : new[] { json.RootElement };
            foreach (var item in items)
            {
                var name = item.GetProperty("Name").GetString() ?? ""; if (!Known(name)) continue;
                var location = item.GetProperty("InstallLocation").GetString() ?? "";
                var manifest = Path.Combine(location, "AppxManifest.xml"); if (!File.Exists(manifest)) continue;
                var doc = XDocument.Load(manifest);
                foreach (var app in doc.Descendants().Where(x => x.Name.LocalName == "Application"))
                {
                    var exe = app.Attribute("Executable")?.Value; if (exe == null) continue;
                    var path = Path.Combine(location, exe);var family=item.GetProperty("PackageFamilyName").GetString()??"";var appId=app.Attribute("Id")?.Value??"";
                    var visual=app.Descendants().FirstOrDefault(x=>x.Name.LocalName=="VisualElements");var logo=visual?.Attribute("Square44x44Logo")?.Value??visual?.Attribute("Square150x150Logo")?.Value??"";
                    if (File.Exists(path)) results.Add(new(name, Path.GetFileName(path), path, "Microsoft Store / Appx"){LaunchKind=LaunchTargetKind.AppUserModelId,LaunchTarget=family+"!"+appId,IconSource=PackageMetadata.ResolveAsset(location,logo)});
                }
            }
        }
        catch { /* Store may be disabled or package metadata inaccessible. */ }
    }
}
