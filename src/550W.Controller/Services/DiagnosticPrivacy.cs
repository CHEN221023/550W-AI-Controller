using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Controller550W.Core;

namespace Controller550W.Controller.Services;

/// <summary>Export uses an allowlist, never regex replacement on arbitrary user text.</summary>
public static class DiagnosticPrivacy
{
    static readonly HashSet<string> Events = new(StringComparer.Ordinal)
    {
        "ControllerStart", "ControllerStop", "StartupFailed", "UnhandledException", "UnobservedTaskException", "UiError", "ConfigWarning", "SettingsSaved", "DiagnosticsExported", "DiagnosticsExportFailed",
        "WatcherStart", "WatcherStop", "StartupUnavailable", "MonitorError", "ReconcileError", "ProcessEventsUnavailable", "WindowEventUnavailable", "ProfileUnavailable", "CommandUnavailable",
        "AnimationError", "AnimationUnavailable", "FIRST_VISUAL", "WEB_READY", "WindowPresentation", "FIRST_FRAME", "BOOT_INTRO_FINISHED", "WAITING_LOOP_READY", "TARGET_READY", "ANIMATION_FINISHED", "USER_SKIP", "WATCHDOG", "CHOREOGRAPHY_COMPLETE", "SHUTDOWN_BEGIN", "VOICE_PLAYING", "BootTargetGone", "Ready", "AnimationExited", "TargetStopped", "BootTrigger", "ShutdownTrigger", "ManagedLaunchSession", "Diagnostics", "DiagnosticUnavailable", "CloseTargetUnavailable", "CloseConfirmationShown", "CloseConfirmationUnavailable", "ManagedCloseState", "ShutdownPreloadReady", "ShutdownPreloadFailed", "ShutdownCancelled", "SHUTDOWN_OVERLAY_VISIBLE", "SHUTDOWN_BEGIN_CONFIRMED", "ShutdownNativeFallback", "ShutdownFallback", "WM_CLOSE", "LaunchExistingWindow", "ManagedLaunchFailed",
        "CORE_HOLD", "LOGO_ASSEMBLING", "LOGO_HOLD", "MULTIWINDOW_REVEAL", "MULTIWINDOW_ACTIVE", "PAGE_READY", "VISUAL_READY", "PRELOAD_READY", "CORE_INITIALIZING", "CORE_DISSOLVING", "CORE_PARTICLES", "PARTICLE_RESIDUE", "LOGO_REVEAL", "LOGO_READY", "LOGO_BURST", "HUD_REVEAL", "HANDOFF_BEGIN", "INTERFACE_HANDOFF", "AUDIO_UNAVAILABLE", "VOICE_UNAVAILABLE"
    };
    static readonly HashSet<string> Exceptions = new(StringComparer.Ordinal)
    {
        "Exception", "InvalidOperationException", "ArgumentException", "ArgumentNullException", "ArgumentOutOfRangeException", "IOException", "UnauthorizedAccessException", "FileNotFoundException", "DirectoryNotFoundException", "TimeoutException", "OperationCanceledException", "TaskCanceledException", "Win32Exception", "COMException", "NullReferenceException", "ObjectDisposedException", "JsonException", "ManagementException", "NotSupportedException", "PlatformNotSupportedException", "AggregateException", "OutOfMemoryException", "WebView2RuntimeNotFoundException", "TypeInitializationException", "DllNotFoundException"
    };
    static readonly string[] NumberKeys = ["latency", "hostElapsed", "elapsed", "confirmElapsed", "fallback", "pids", "windows", "profiles", "hresult"];
    static readonly string[] BooleanKeys = ["processEvents", "sent", "firstRun", "paused"];
    static readonly string[] AnimationKeys = ["durationMultiplier", "centerRevealMs", "minimumBootMs", "diagnosticsMinimumMs", "readyMs", "shutdownMs", "fadeMs", "bootWatchdogMs", "shutdownWatchdogMs", "allowEscape", "allowClickSkip", "waitForReady", "coreHoldMs", "coreDissolveMs", "particleResidueMs", "logoRevealMs", "logoHoldMs", "logoBurstMs", "particleCount", "particleSize", "particleSpread", "multiWindowRevealMs", "handoffCompressMs"];
    static readonly string[] AudioKeys = ["soundEnabled", "soundVolume", "voiceEnabled", "voiceVolume", "muted", "bootVoice", "shutdownVoice"];
    static readonly string[] ProfileKeys = ["enabled", "useIndependentSettings", "appLaunchOffsetMs", "applyPresentationToPassive", "startupGraceMs", "optimizedShutdown", "closeConfirmation", "hardwareDiagnostics", "networkDiagnostics", "allowMinimizedReady", "readyDelayMs", "minimumBootTimeMs", "cooldownMs", "matchExecutablePath"];

    public static string? SanitizeLogLine(string line)
    {
        var match = Regex.Match(line, @"^(?<time>\S+)\s+\[(?<event>[^\]\r\n]{1,80})\]\s*(?<detail>.*)$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        if (!match.Success || !DateTimeOffset.TryParse(match.Groups["time"].Value, out var timestamp)) return null;
        var sourceEvent = match.Groups["event"].Value;
        var known = Events.Contains(sourceEvent);
        var name = known ? sourceEvent : "OtherEvent";
        var levelMatch = Regex.Match(match.Groups["detail"].Value, @"(?:^|,\s*)level=(INFO|WARN|ERROR)(?:,|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        var level = levelMatch.Success ? levelMatch.Groups[1].Value : sourceEvent.Contains("Error") || sourceEvent.Contains("Failed") ? "ERROR" : sourceEvent.Contains("Unavailable") || sourceEvent.Contains("Fallback") || sourceEvent == "WATCHDOG" ? "WARN" : "INFO";
        var fields = new List<string>();
        // Unknown event names can themselves contain user text; none of their details survive.
        if (known)
        {
            var detail = match.Groups["detail"].Value;
            foreach (var key in NumberKeys)
            {
                var value = Regex.Match(detail, @"(?:^|,\s*)" + key + @"=(-?\d{1,12}(?:\.\d{1,3})?)(?:ms|s)?(?:,|\s|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                if (value.Success) fields.Add(key + "=" + value.Groups[1].Value);
            }
            foreach (var key in BooleanKeys)
            {
                var value = Regex.Match(detail, @"(?:^|,\s*)" + key + @"=(True|False|true|false)(?:,|\s|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                if (value.Success) fields.Add(key + "=" + value.Groups[1].Value.ToLowerInvariant());
            }
            var exception = Regex.Match(detail, @"(?:exception=|,\s*|^)([A-Za-z]+Exception)(?::|,|\s|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            if (exception.Success && Exceptions.Contains(exception.Groups[1].Value)) fields.Add("exception=" + exception.Groups[1].Value);
            var mode = Regex.Match(detail, @"(?:^|,\s*)mode=(optimized|preview|passive)(?:,|\s|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            if (mode.Success) fields.Add("mode=" + mode.Groups[1].Value);
            if (name == "ManagedCloseState")
            {
                var phase = Regex.Match(detail, @",\s*(Running|CloseRequested|ShutdownPreparing|WaitingConfirmation|ShutdownAnimating|AppClosing|Stopped)(?:,|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                if (phase.Success) fields.Add("phase=" + phase.Groups[1].Value);
            }
        }
        return $"{timestamp.ToUniversalTime():O} [{level}] [{name}]" + (fields.Count == 0 ? "" : " " + string.Join(", ", fields));
    }

    public static object Configuration(ControllerSettings settings)
    {
        using var json = JsonDocument.Parse(WireJson.Serialize(settings));
        var root = json.RootElement;
        var result = SafeValues(root, ["schemaVersion", "startWithWindows", "paused", "hardwareDiagnostics", "fallbackPollingSeconds"]);
        result["animation"] = Animation(root.GetProperty("animation"));
        result["audio"] = Audio(root.GetProperty("audio"));
        if (root.TryGetProperty("network", out var network))
        {
            var safe = SafeValues(network, ["enabled", "timeoutMs"]);
            safe["domesticEndpointCount"] = Count(network, "domesticEndpoints");
            safe["externalEndpointCount"] = Count(network, "externalEndpoints");
            EnumValue(safe, network, "vpnMode", ["auto", "manual", "disabled"]);
            result["network"] = safe;
        }
        var profiles = new List<object>();
        if (root.TryGetProperty("profiles", out var array) && array.ValueKind == JsonValueKind.Array)
            foreach (var profile in array.EnumerateArray())
            {
                var safe = SafeValues(profile, ProfileKeys);
                safe["profileNumber"] = profiles.Count + 1;
                EnumValue(safe, profile, "startTrigger", ["process_start", "window_show"]);
                EnumValue(safe, profile, "stopTrigger", ["process_exit", "window_hide"]);
                EnumValue(safe, profile, "launchMode", ["passive_detection", "optimized_shortcut"]);
                EnumValue(safe, profile, "launchKind", ["executable", "shortcut", "app_user_model_id", "uri_protocol", "shell_apps_folder"]);
                EnumValue(safe, profile, "windowPresentation", ["preserve", "maximize", "center", "fill_work_area"]);
                EnumValue(safe, profile, "presentationTiming", ["after_stable", "halfway", "before_handoff"]);
                EnumValue(safe, profile, "closeEntry", ["managed_close", "auto_caption_proxy"]);
                if (profile.TryGetProperty("animationOverrides", out var animation) && animation.ValueKind == JsonValueKind.Object) safe["animationOverrides"] = Animation(animation);
                if (profile.TryGetProperty("audioOverrides", out var audio) && audio.ValueKind == JsonValueKind.Object) safe["audioOverrides"] = Audio(audio);
                safe["processRuleCount"] = Count(profile, "processNames");
                profiles.Add(safe);
            }
        result["profiles"] = profiles;
        return result;
    }
    static Dictionary<string, object?> Animation(JsonElement source)
    {
        var result = SafeValues(source, AnimationKeys);
        EnumValue(result, source, "colorProfile", ["movie_red", "amber", "green", "cyan", "white"]);
        EnumValue(result, source, "preset", ["Standard", "Quick", "Cinematic"]);
        EnumValue(result, source, "monitor", ["target_window", "cursor", "primary", "specified"]);
        return result;
    }
    static Dictionary<string, object?> Audio(JsonElement source)
    {
        var result = SafeValues(source, AudioKeys);
        result["customSoundCount"] = Count(source, "audioFiles");
        result["customVoiceCount"] = Count(source, "voiceFiles");
        result["voicePhraseCount"] = Count(source, "phrases");
        result["hasNamedVoice"] = source.TryGetProperty("voiceName", out var value) && value.ValueKind == JsonValueKind.String && value.GetString()?.Length > 0;
        return result;
    }
    static int Count(JsonElement source, string key) => source.TryGetProperty(key, out var value) ? value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().Count() : 0 : 0;
    static Dictionary<string, object?> SafeValues(JsonElement source, string[] keys)
    {
        var result = new Dictionary<string, object?>();
        foreach (var key in keys)
            if (source.TryGetProperty(key, out var value))
            {
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) result[key] = value.GetBoolean();
                else if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)) result[key] = number;
            }
        return result;
    }
    static void EnumValue(Dictionary<string, object?> result, JsonElement source, string key, string[] allowed)
    {
        if (source.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && allowed.Contains(value.GetString())) result[key] = value.GetString();
    }
}
