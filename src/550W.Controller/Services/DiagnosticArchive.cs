using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Controller550W.Core;

namespace Controller550W.Controller.Services;

public static class DiagnosticArchive
{
    public static string Create(string destination, string dataDirectory, ControllerSettings settings, object systemInformation, CancellationToken cancellationToken = default)
    {
        destination = Path.GetFullPath(destination);
        if (!destination.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) destination += ".zip";
        var parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("请选择有效的导出位置。");
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException();
        var temporary = Path.Combine(parent, ".550w-diagnostic-" + Guid.NewGuid().ToString("N") + ".tmp");
        var logs = ReadLogs(dataDirectory, false, cancellationToken);
        var crashes = ReadLogs(dataDirectory, true, cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Add(archive, "app.log", string.Join(Environment.NewLine, logs) + Environment.NewLine);
                if (crashes.Count > 0) Add(archive, "crash.log", string.Join(Environment.NewLine, crashes) + Environment.NewLine);
                Add(archive, "recent-animation.log", string.Join(Environment.NewLine, logs.Where(IsAnimationEvent).TakeLast(1200)) + Environment.NewLine);
                Add(archive, "system-info.json", JsonSerializer.Serialize(systemInformation, new JsonSerializerOptions { WriteIndented = true }));
                Add(archive, "config-redacted.json", JsonSerializer.Serialize(DiagnosticPrivacy.Configuration(settings), new JsonSerializerOptions { WriteIndented = true }));
                Add(archive, "README.txt", "550W AI Controller 故障诊断包\n\napp.log：本次控制器启动以来的事件、错误和警告（最多保留最近 5000 条）。\ncrash.log：如存在，包含异常类型和错误编号，不含异常正文或用户路径。\nrecent-animation.log：最近的启动、声音、交接与关闭事件。\nsystem-info.json：软件与 Windows 版本、运行库、基础硬件及显示器/DPI 信息。\nconfig-redacted.json：匿名应用编号和允许导出的配置参数。\n\n隐私处理：未收集聊天内容。已排除应用名称和标识、进程名称、窗口标题、文件路径、启动参数、网址、网卡标识、语音内容与用户名称。原始日志不会直接放入 ZIP；仅导出白名单事件与数值/状态。\n未知事件显示为 OtherEvent，未知字段省略。部分系统信息不可读取时记为 unavailable，导出仍可完成。\n");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
            return destination;
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
    }

    static void Add(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
    static bool IsAnimationEvent(string line) => new[] { "BOOT", "CORE", "LOGO", "PARTICLE", "HUD", "VISUAL", "FRAME", "READY", "HANDOFF", "SHUTDOWN", "VOICE", "AUDIO", "Animation", "Close", "Launch", "Boot", "Shutdown", "CHOREOGRAPHY", "WAITING", "WM_CLOSE", "WATCHDOG", "USER_SKIP", "TargetStopped" }.Any(line.Contains);

    static List<string> ReadLogs(string dataDirectory, bool crash, CancellationToken cancellationToken)
    {
        var result = new List<string>();
        var directory = new DirectoryInfo(Path.Combine(dataDirectory, "Logs"));
        try
        {
            if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0) return result;
            var files = directory.GetFiles(crash ? "crash*.log" : "controller-*.log", SearchOption.TopDirectoryOnly)
                .Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0 && (!crash || f.Name is "crash.log" or "crash-previous.log"))
                .OrderByDescending(f => f.LastWriteTimeUtc).Take(crash ? 2 : 8).Reverse();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var buffer = new byte[(int)Math.Min(input.Length, 768 * 1024)];
                    var read = input.ReadAtLeast(buffer, buffer.Length, false);
                    foreach (var line in Encoding.UTF8.GetString(buffer, 0, read).Split('\n'))
                    {
                        if (line.Length > 4096) continue;
                        var safe = DiagnosticPrivacy.SanitizeLogLine(line.TrimEnd('\r'));
                        if (safe == null) continue;
                        if (!crash && DateTimeOffset.TryParse(safe.Split(' ')[0], out var timestamp) && timestamp < LogService.SessionStartedUtc.AddSeconds(-1)) continue;
                        result.Add(safe);
                    }
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
        return result.TakeLast(crash ? 600 : 5000).ToList();
    }
}
