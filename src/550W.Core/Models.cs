using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Controller550W.Core;

public enum StartTrigger { ProcessStart, WindowShow }
public enum StopTrigger { ProcessExit, WindowHide }
public enum MonitorTarget { TargetWindow, Cursor, Primary, Specified }
public enum RuntimePhase { NotRunning, Starting, Visible, Hidden, Stopping, Stopped }
public enum LaunchMode { PassiveDetection, OptimizedShortcut }
public enum LaunchTargetKind { Executable, Shortcut, AppUserModelId, UriProtocol, ShellAppsFolder }
public enum WindowPresentation { Preserve, Maximize, Center, FillWorkArea }
public enum PresentationTiming { AfterStable, Halfway, BeforeHandoff }
public enum CloseEntry { ManagedClose, AutoCaptionProxy }
public enum AnimationKind { Boot, Shutdown }
public enum VpnMode { Auto, Manual, Disabled }

public sealed class AppProfile : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    string displayName = "AI 应用", runtimeStatus = "未运行";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get => displayName; set { displayName = value; PropertyChanged?.Invoke(this, new(nameof(DisplayName))); } }
    public string[] ProcessNames { get; set; } = [];
    public string ExecutablePath { get; set; } = "";
    public bool MatchExecutablePath { get; set; }
    public string WindowTitleMatch { get; set; } = "";
    public StartTrigger StartTrigger { get; set; } = StartTrigger.WindowShow;
    public StopTrigger StopTrigger { get; set; } = StopTrigger.WindowHide;
    public string BootTheme { get; set; } = "550W";
    public string ShutdownTheme { get; set; } = "550W";
    public LaunchMode LaunchMode { get; set; }
    public LaunchTargetKind LaunchKind { get; set; }
    public string LaunchTarget { get; set; } = "";
    public string LaunchArguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string SourceShortcut { get; set; } = "";
    public string IconSource { get; set; } = "";
    public string IconCachePath { get; set; } = "";
    public string OptimizedShortcutPath { get; set; } = "";
    public int AppLaunchOffsetMs { get; set; } = 300;
    public WindowPresentation WindowPresentation { get; set; } = WindowPresentation.Maximize;
    public PresentationTiming PresentationTiming { get; set; } = PresentationTiming.AfterStable;
    public bool ApplyPresentationToPassive { get; set; }
    public string StartupCorePath { get; set; } = "";
    public string CoreCachePath { get; set; } = "";
    public bool UseIndependentSettings { get; set; }
    public AnimationSettings AnimationOverrides { get; set; } = new();
    public AudioSettings AudioOverrides { get; set; } = new();
    // Retained only to read older configuration files. V3 runtime uses the resolver.
    public string ColorProfile { get; set; } = "";
    public int StartupGraceMs { get; set; } = 2500;
    public bool OptimizedShutdown { get; set; }
    public bool CloseConfirmation { get; set; } = true;
    public CloseEntry CloseEntry { get; set; }
    public bool Enabled { get; set; } = true;
    public bool HardwareDiagnostics { get; set; } = true;
    public bool NetworkDiagnostics { get; set; } = true;
    public bool AllowMinimizedReady { get; set; }
    public int ReadyDelayMs { get; set; } = 350;
    public int MinimumBootTimeMs { get; set; } = 4000;
    public int CooldownMs { get; set; } = 650;
    [JsonIgnore] public string ProcessNamesText
    {
        get => string.Join(", ", ProcessNames);
        set { ProcessNames = value.Split([',', ';', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); PropertyChanged?.Invoke(this, new(nameof(ProcessNamesText))); }
    }
    [JsonIgnore] public string RuntimeStatus { get => runtimeStatus; set { if (runtimeStatus == value) return; runtimeStatus = value; PropertyChanged?.Invoke(this, new(nameof(RuntimeStatus))); } }
    [JsonIgnore] public string LastMonitorDevice { get; set; } = "";
    // Process.ProcessName removes .exe; a dot inside the name is not an extension.
    public static string NormalizeProcessName(string name)
    {
        var file = Path.GetFileName(name);
        return file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
    }
    public bool HasProcessName(string name) => ProcessNames.Any(x => string.Equals(NormalizeProcessName(x), NormalizeProcessName(name), StringComparison.OrdinalIgnoreCase));
    public bool MatchesProcess(string name, string? path = null)
    {
        if (!HasProcessName(name)) return false;
        if (!MatchExecutablePath || ExecutablePath.Length == 0) return true;
        return path != null && string.Equals(path, ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AnimationSettings
{
    public string Preset { get; set; } = "Standard";
    public double DurationMultiplier { get; set; } = 1;
    public string ColorProfile { get; set; } = "movie_red";
    public string SystemLabel { get; set; } = "550W";
    public int CenterRevealMs { get; set; } = 900;
    public int CoreHoldMs { get; set; } = 650;
    public int CoreDissolveMs { get; set; } = 650;
    public int ParticleResidueMs { get; set; } = 350;
    // Includes the idle interval. LogoRevealMs remains only for pre-V4.5 imports.
    public int ParticleToLogoMs { get; set; } = 1200;
    public int LogoRevealMs { get; set; } = 850;
    public int LogoHoldMs { get; set; } = 450;
    public int LogoBurstMs { get; set; } = 900;
    public int ParticleCount { get; set; } = 700;
    public double ParticleSize { get; set; } = 2.2;
    public double ParticleSpread { get; set; } = 1;
    public int MultiWindowRevealMs { get; set; } = 850;
    public int HandoffCompressMs { get; set; } = 420;
    public int MinimumBootMs { get; set; } = 4000;
    public int DiagnosticsMinimumMs { get; set; } = 1400;
    public int ReadyMs { get; set; } = 600;
    public int ShutdownMs { get; set; } = 2600;
    public int FadeMs { get; set; } = 300;
    public int BootWatchdogMs { get; set; } = 20000;
    public int ShutdownWatchdogMs { get; set; } = 5000;
    public bool AllowEscape { get; set; } = true;
    public bool AllowClickSkip { get; set; }
    public bool WaitForReady { get; set; } = true;
    public MonitorTarget Monitor { get; set; } = MonitorTarget.TargetWindow;
    public string MonitorDevice { get; set; } = "";
    public bool Sound { get; set; } // Original theme has no sound assets.
    public int Scale(int milliseconds) => (int)Math.Round(milliseconds * DurationMultiplier);
    public string? ParticleTimingError()
    {
        if (ParticleToLogoMs <= ParticleResidueMs) return "粒子 → 550C 总时长必须大于中央粒子待机时长。";
        if (ParticleResidueMs is < 0 or > 10000) return "中央粒子待机时长应为 0～10000 毫秒。";
        if (ParticleToLogoMs is < 1 or > 20000) return "粒子 → 550C 总时长应为 1～20000 毫秒。";
        if (LogoHoldMs is < 0 or > 10000) return "550C 完整停留时长应为 0～10000 毫秒。";
        return null;
    }
    public void ApplyPreset(string preset)
    {
        Preset = preset;
        DurationMultiplier = preset switch { "Quick" => .75, "Cinematic" => 1.5, _ => 1 };
    }
    public void Validate()
    {
        DurationMultiplier = double.IsFinite(DurationMultiplier) ? Math.Clamp(DurationMultiplier, .5, 2) : 1;
        CenterRevealMs = Math.Clamp(CenterRevealMs, 100, 2000);
        CoreHoldMs = Math.Clamp(CoreHoldMs, 100, 5000);
        CoreDissolveMs = Math.Clamp(CoreDissolveMs, 100, 3000);
        // These three user timings are validated together before saving; never silently
        // clamp an edited combination or add a hidden minimum regroup interval.
        LogoRevealMs = Math.Clamp(LogoRevealMs, 100, 3000);
        LogoBurstMs = Math.Clamp(LogoBurstMs, 100, 3000);
        ParticleCount = Math.Clamp(ParticleCount, 100, 2000);
        ParticleSize = double.IsFinite(ParticleSize) ? Math.Clamp(ParticleSize, .5, 6) : 2.2;
        ParticleSpread = double.IsFinite(ParticleSpread) ? Math.Clamp(ParticleSpread, .25, 2) : 1;
        MultiWindowRevealMs = Math.Clamp(MultiWindowRevealMs, 100, 3000);
        HandoffCompressMs = Math.Clamp(HandoffCompressMs, 100, 1500);
        MinimumBootMs = Math.Clamp(MinimumBootMs, 3000, 15000);
        DiagnosticsMinimumMs = Math.Clamp(DiagnosticsMinimumMs, 100, 12000);
        ReadyMs = Math.Clamp(ReadyMs, 100, 2500);
        ShutdownMs = Math.Clamp(ShutdownMs, 500, 4000);
        FadeMs = Math.Clamp(FadeMs, 100, 1000);
        BootWatchdogMs = Math.Clamp(BootWatchdogMs, 5000, 60000);
        ShutdownWatchdogMs = Math.Clamp(ShutdownWatchdogMs, 3000, 10000);
        SystemLabel = string.IsNullOrWhiteSpace(SystemLabel) ? "550W" : SystemLabel[..Math.Min(SystemLabel.Length, 20)];
        if (!new[] { "movie_red", "amber", "green", "cyan", "white" }.Contains(ColorProfile)) ColorProfile = "movie_red";
        MonitorDevice ??= "";
    }
}

public sealed class NetworkSettings
{
    public bool Enabled { get; set; } = true;
    public int TimeoutMs { get; set; } = 2000;
    public string DnsHost { get; set; } = "www.microsoft.com";
    public string[] DomesticEndpoints { get; set; } = ["https://www.baidu.com/", "https://www.qq.com/"];
    public string[] ExternalEndpoints { get; set; } = ["https://www.microsoft.com/", "https://www.cloudflare.com/cdn-cgi/trace"];
    public VpnMode VpnMode { get; set; } = VpnMode.Auto;
    public string VpnAdapterId { get; set; } = "";
    [JsonIgnore] public string DomesticText { get => string.Join(Environment.NewLine, DomesticEndpoints); set => DomesticEndpoints = Split(value); }
    [JsonIgnore] public string ExternalText { get => string.Join(Environment.NewLine, ExternalEndpoints); set => ExternalEndpoints = Split(value); }
    static string[] Split(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed class AudioSettings
{
    public bool SoundEnabled { get; set; } = true;
    public int SoundVolume { get; set; } = 70;
    public bool VoiceEnabled { get; set; }
    public int VoiceVolume { get; set; } = 60;
    public bool Muted { get; set; }
    public bool BootVoice { get; set; } = true;
    public bool ShutdownVoice { get; set; } = true;
    public string VoiceName { get; set; } = "";
    public Dictionary<string, string> AudioFiles { get; set; } = new();
    public Dictionary<string, string> VoiceFiles { get; set; } = new();
    public Dictionary<string, string> Phrases { get; set; } = new()
    {
        ["BOOT"] = "{system} 系统启动。", ["CORE"] = "核心模块初始化。", ["NETWORK"] = "网络连接已建立。",
        ["PROCESS"] = "目标人工智能进程已检测。", ["READY"] = "{app} 界面已就绪。系统就绪。",
        ["SHUTDOWN"] = "正在终止人工智能会话。", ["NETWORK_CLOSED"] = "网络连接关闭。",
        ["STANDBY"] = "核心模块进入待机。", ["POWER_OFF"] = "{system} 系统关闭。"
    };
    public void Validate()
    {
        SoundVolume = Math.Clamp(SoundVolume, 0, 100);
        VoiceVolume = Math.Clamp(VoiceVolume, 0, 100);
        Phrases ??= new(); AudioFiles ??= new(); VoiceFiles ??= new(); VoiceName ??= "";
    }
}

public sealed class ControllerSettings
{
    public int SchemaVersion { get; set; } = 4;
    public bool StartWithWindows { get; set; } = true;
    public bool Paused { get; set; }
    public bool FirstRunComplete { get; set; }
    public bool HardwareDiagnostics { get; set; } = true;
    public int FallbackPollingSeconds { get; set; } = 5;
    public ObservableCollection<AppProfile> Profiles { get; set; } = [];
    public AnimationSettings Animation { get; set; } = new();
    public NetworkSettings Network { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public string? ParticleTimingError()
    {
        if (Animation?.ParticleTimingError() is { } globalError) return "全局动画：" + globalError;
        foreach (var profile in Profiles.Where(p => p.UseIndependentSettings))
            if (profile.AnimationOverrides?.ParticleTimingError() is { } error) return profile.DisplayName + "：" + error;
        return null;
    }
    public void Validate()
    {
        Profiles ??= []; Animation ??= new(); Network ??= new(); Audio ??= new();
        Audio.Validate(); Animation.Validate();
        FallbackPollingSeconds = Math.Clamp(FallbackPollingSeconds, 3, 60);
        Network.TimeoutMs = Math.Clamp(Network.TimeoutMs, 300, 5000);
        Network.DomesticEndpoints = FilterEndpoints(Network.DomesticEndpoints);
        Network.ExternalEndpoints = FilterEndpoints(Network.ExternalEndpoints);
        var ids = new HashSet<string>();
        foreach (var p in Profiles)
        {
            if (string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id)) { p.Id = Guid.NewGuid().ToString("N"); ids.Add(p.Id); }
            p.DisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? "AI 应用" : p.DisplayName[..Math.Min(p.DisplayName.Length, 60)];
            p.ProcessNames ??= [];
            p.ExecutablePath ??= ""; p.LaunchTarget ??= ""; p.LaunchArguments ??= ""; p.WorkingDirectory ??= ""; p.WindowTitleMatch ??= "";
            p.SourceShortcut ??= ""; p.IconSource ??= ""; p.IconCachePath ??= ""; p.OptimizedShortcutPath ??= "";
            p.StartupCorePath ??= ""; p.CoreCachePath ??= ""; p.ColorProfile ??= "";
            p.AnimationOverrides ??= new(); p.AudioOverrides ??= new();
            p.AnimationOverrides.Validate(); p.AudioOverrides.Validate();
            p.ReadyDelayMs = Math.Clamp(p.ReadyDelayMs, 200, 10000);
            p.MinimumBootTimeMs = Math.Clamp(p.MinimumBootTimeMs, 3000, 15000);
            p.CooldownMs = Math.Clamp(p.CooldownMs, 0, 5000);
            p.StartupGraceMs = Math.Clamp(p.StartupGraceMs, 300, 8000);
            p.AppLaunchOffsetMs = Math.Clamp(p.AppLaunchOffsetMs, -1000, 5000);
            if (p.ColorProfile.Length > 0 && !new[] { "movie_red", "amber", "green", "cyan", "white" }.Contains(p.ColorProfile)) p.ColorProfile = "";
        }
    }
    static string[] FilterEndpoints(string[]? values) => (values ?? []).Where(x => Uri.TryCreate(x, UriKind.Absolute, out var u) && u.Scheme == "https" && string.IsNullOrEmpty(u.UserInfo)).Distinct().Take(5).ToArray();
}

public static class WireJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        WriteIndented = false
    };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(Serialize(value), Options)!;
}

public sealed record DiagnosticRow(string Key, string Label, string Value, string Status = "unknown", string Detail = "");
public sealed record TargetStatus(string Name, bool ProcessDetected, bool WindowDetected, bool Ready, string State, long WindowHandle = 0);
public sealed class AnimationSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public AnimationKind Kind { get; set; }
    public string DisplayName { get; set; } = "演示应用";
    public string Theme { get; set; } = "550W";
    public AnimationSettings Settings { get; set; } = new();
    public int MinimumBootTimeMs { get; set; } = 4000;
    public bool Demo { get; set; }
    public int MonitorX { get; set; }
    public int MonitorY { get; set; }
    public int MonitorWidth { get; set; } = 1920;
    public int MonitorHeight { get; set; } = 1080;
    public string DataDirectory { get; set; } = "";
    public AudioSettings Audio { get; set; } = new();
    public string AudioTest { get; set; } = "";
    public string CoreImagePath { get; set; } = "";
    public bool DebugMode { get; set; }
    public int HostElapsedMs { get; set; }
    // One monotonic origin across native preparation and the WebView intro.
    public long ParticleStartTimestamp { get; set; }
    public double ParticleElapsedMs { get; set; }
    public long ParticleClockSentUnixMs { get; set; }
    public bool Preload { get; set; }
}
