using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Controller550W.Animation;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        string? Option(string key) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        if(args.Contains("--render-core")&&Option("--input") is {} input&&Option("--output") is {} output){try{CoreAssetRenderer.Run(input,output);}catch{}return;}
        var pipeName = Option("--pipe"); var parentText = Option("--parent");
        if (pipeName == null || !pipeName.StartsWith("550W-", StringComparison.Ordinal) || !int.TryParse(parentText, out var parent)) return;
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            pipe.Connect(5000);
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
            var first = reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            using var init = JsonDocument.Parse(first!);
            var session = init.RootElement.GetProperty("session").Deserialize<AnimationSession>(WireJson.Options)!;
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (_, e) =>
            {
                e.Handled = true;
                try { writer.WriteLine(WireJson.Serialize(new { type = "error", message = "动画界面异常：" + e.Exception.GetType().Name })); } catch { }
                app.Shutdown(1);
            };
            var window = new AnimationWindow(session, parent, reader, writer); app.Run(window);
        }
        catch { /* Missing controller, IPC failure or malformed session: exit without any overlay. */ }
    }
}
