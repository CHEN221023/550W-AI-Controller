namespace Controller550W.Core;

/// <summary>One source of truth for previews, launches and managed shutdown sessions.</summary>
public static class ProfileSettingsResolver
{
    public static AnimationSettings ResolveAnimation(ControllerSettings settings, AppProfile profile)
    {
        var result = WireJson.Clone((profile.UseIndependentSettings ? profile.AnimationOverrides : settings.Animation) ?? settings.Animation ?? new());
        result.Validate();
        return result;
    }

    public static AudioSettings ResolveAudio(ControllerSettings settings, AppProfile profile)
    {
        var result = WireJson.Clone((profile.UseIndependentSettings ? profile.AudioOverrides : settings.Audio) ?? settings.Audio ?? new());
        result.Validate();
        result.Muted |= settings.Audio?.Muted == true;
        return result;
    }

    /// <summary>Start an override from what the user currently sees, rather than constructor defaults.</summary>
    public static void EnableIndependent(ControllerSettings settings, AppProfile profile)
    {
        if (profile.UseIndependentSettings) return;
        profile.AnimationOverrides = ResolveAnimation(settings, profile);
        profile.AudioOverrides = ResolveAudio(settings, profile);
        profile.UseIndependentSettings = true;
    }

    /// <summary>
    /// Called only after the settings window confirms the operation. The explicit allowlist
    /// deliberately excludes application identity, launch targets, matching and shortcut metadata.
    /// All receivers get separate snapshots so changing one application cannot change another.
    /// </summary>
    public static int ApplyToAll(ControllerSettings settings, AppProfile source)
    {
        var animation = ResolveAnimation(settings, source);
        if (animation.ParticleTimingError() is { } error) throw new ArgumentException(error);
        var audio = ResolveAudio(settings, source);
        foreach (var destination in settings.Profiles)
        {
            destination.AnimationOverrides = WireJson.Clone(animation);
            destination.AudioOverrides = WireJson.Clone(audio);
            destination.UseIndependentSettings = true;
            destination.BootTheme = source.BootTheme;
            destination.ShutdownTheme = source.ShutdownTheme;
            destination.StartupCorePath = source.StartupCorePath;
            destination.CoreCachePath = source.CoreCachePath;
            destination.WindowPresentation = source.WindowPresentation;
            destination.PresentationTiming = source.PresentationTiming;
            destination.ApplyPresentationToPassive = source.ApplyPresentationToPassive;
            destination.AppLaunchOffsetMs = source.AppLaunchOffsetMs;
            destination.StartTrigger = source.StartTrigger;
            destination.StopTrigger = source.StopTrigger;
            destination.StartupGraceMs = source.StartupGraceMs;
            destination.OptimizedShutdown = source.OptimizedShutdown;
            destination.CloseConfirmation = source.CloseConfirmation;
            destination.CloseEntry = source.CloseEntry;
            destination.HardwareDiagnostics = source.HardwareDiagnostics;
            destination.NetworkDiagnostics = source.NetworkDiagnostics;
            destination.AllowMinimizedReady = source.AllowMinimizedReady;
            destination.ReadyDelayMs = source.ReadyDelayMs;
            destination.CooldownMs = source.CooldownMs;
            // Keep legacy exports understandable without letting these fields affect resolution.
            destination.ColorProfile = animation.ColorProfile;
            destination.MinimumBootTimeMs = animation.MinimumBootMs;
        }
        settings.Validate();
        return settings.Profiles.Count;
    }
}
