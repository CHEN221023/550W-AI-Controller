using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Controller550W.Controller.Native;

namespace Controller550W.Controller.Services;

internal sealed class AnimationHostSession : IAsyncDisposable
{
    readonly AnimationSession session; readonly CancellationTokenSource life; readonly NamedPipeServerStream pipe; readonly SemaphoreSlim writes = new(1);
    Process? process; AnimationJob? job; StreamWriter? writer; Task? messages;
    public readonly TaskCompletionSource Prepared = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource Visual = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource ShutdownBegan = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public event Action<string, string>? Message;
    public Task Completion { get; private set; } = Task.CompletedTask;
    public bool Available => process != null && !process.HasExited && writer != null;
    public AnimationHostSession(AnimationSession session, CancellationToken token)
    {
        this.session = session; life = CancellationTokenSource.CreateLinkedTokenSource(token);
        pipe = new("550W-" + session.SessionId, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }
    public async Task StartAsync()
    {
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "550W.Animation.exe")) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("--pipe"); info.ArgumentList.Add("550W-" + session.SessionId); info.ArgumentList.Add("--parent"); info.ArgumentList.Add(Environment.ProcessId.ToString());
        process = Process.Start(info) ?? throw new IOException("无法启动动画程序"); job = new AnimationJob(process);
        Completion = process.WaitForExitAsync(life.Token);
        await pipe.WaitForConnectionAsync(life.Token).WaitAsync(TimeSpan.FromSeconds(6));
        writer = new(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true }; await Send(new { type = "init", session });
        messages = Read();
    }
    async Task Read()
    {
        try
        {
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
            while (await reader.ReadLineAsync(life.Token) is { } line)
            {
                using var doc = JsonDocument.Parse(line); var root = doc.RootElement; var type = root.GetProperty("type").GetString() ?? "";
                var detail = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                if (type == "PRELOAD_READY") Prepared.TrySetResult();
                if (type == "VISUAL_READY") Visual.TrySetResult();
                if (type == "SHUTDOWN_BEGIN") ShutdownBegan.TrySetResult();
                Message?.Invoke(type, detail);
            }
        }
        catch (OperationCanceledException) { } catch (Exception e) { Message?.Invoke("error", e.GetType().Name); }
        finally { Prepared.TrySetException(new IOException("动画准备结束")); Visual.TrySetException(new IOException("动画视图结束")); ShutdownBegan.TrySetException(new IOException("动画未开始")); }
    }
    public async Task Send(object message)
    {
        if (writer == null || life.IsCancellationRequested) return;
        await writes.WaitAsync(life.Token); try { await writer.WriteLineAsync(WireJson.Serialize(message).AsMemory(), life.Token); } finally { writes.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        life.Cancel();
        try { pipe.Dispose(); } catch { }
        // Kill/Job is exclusively for our own Animation process tree, never for a target client.
        if (process != null) { try { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1)); } } catch { } process.Dispose(); }
        job?.Dispose(); life.Dispose();
        try
        {
            var root=Path.GetFullPath(Path.Combine(session.DataDirectory,"WebView2"));var path=Path.GetFullPath(Path.Combine(root,session.SessionId));
            if(session.SessionId.Length==32&&session.SessionId.All(Uri.IsHexDigit)&&path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))Directory.Delete(path,true);
        }catch{ /* A closing renderer may briefly retain a cache file; it is safe to remove on next startup. */ }
    }
}
