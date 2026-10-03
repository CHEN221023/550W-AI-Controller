using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Diagnostics;

namespace Controller550W.Animation;

/// <summary>Ephemeral offline machine synthesis and optional installed Windows SAPI voice.</summary>
internal sealed class VoiceService(AudioSettings settings) : IDisposable
{
    object? voice;
    bool failed;
    public bool SystemAvailable=>!failed&&voice!=null;
    public async Task<string?> RenderOfflineAsync(string text,string directory,CancellationToken token)
    {
        if(settings.Muted||!settings.VoiceEnabled||text.Length==0)return null;
        var worker=Path.Combine(Path.GetDirectoryName(typeof(VoiceService).Assembly.Location)!,"VoiceEngines","espeak-ng","550W.VoiceWorker.exe");if(!File.Exists(worker))return null;
        var key=Guid.NewGuid().ToString("N");var input=Path.Combine(directory,key+".txt");var output=Path.Combine(directory,key+".wav");
        try{
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(input,text[..Math.Min(180,text.Length)],token);var info=new ProcessStartInfo(worker){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(worker)!};foreach(var a in new[]{"--input",input,"--output",output})info.ArgumentList.Add(a);
            using var process=Process.Start(info);if(process==null)return null;
            try{await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(2),token);}catch{try{process.Kill();}catch{}return null;}
            return process.ExitCode==0&&File.Exists(output)&&new FileInfo(output).Length>44?output:null;
        }catch{return null;}finally{try{File.Delete(input);}catch{}}
    }
    public async Task SpeakAsync(string text, bool priority, CancellationToken token)
    {
        if (failed || settings.Muted || !settings.VoiceEnabled || text.Length == 0) return;
        try
        {
            if (voice == null)
            {
                var type = Type.GetTypeFromProgID("SAPI.SpVoice"); if (type == null) { failed = true; return; }
                voice = Activator.CreateInstance(type); dynamic created = voice!;
                created.Volume = settings.VoiceVolume; created.Rate = -1;
                if (settings.VoiceName.Length > 0)
                {
                    dynamic voices = created.GetVoices();
                    for (var i = 0; i < voices.Count; i++) { dynamic candidate = voices.Item(i); if (((string)candidate.GetDescription()).Contains(settings.VoiceName, StringComparison.OrdinalIgnoreCase)) { created.Voice = candidate; break; } }
                }
            }
            dynamic synth = voice!;
            if (!priority && (int)synth.Status.RunningState == 2) return;
            // Async + optional purge + explicit plain text: configurable phrases are never parsed as XML.
            synth.Speak(text[..Math.Min(180, text.Length)], 1 | 16 | (priority ? 2 : 0));
            var deadline = DateTime.UtcNow.AddMilliseconds(2600);
            do { await Task.Delay(80, token); } while ((int)synth.Status.RunningState == 2 && DateTime.UtcNow < deadline);
        }
        catch (OperationCanceledException) { }
        catch { failed = true; }
    }
    public void Dispose()
    {
        if (voice == null) return;
        try { dynamic synth = voice; synth.Speak("", 3); } catch { }
        try { Marshal.FinalReleaseComObject(voice); } catch { } voice = null;
    }
}
