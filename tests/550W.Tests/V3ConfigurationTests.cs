using System.Text.Json;
using Controller550W.Core;
using Xunit;

namespace Controller550W.Tests;

public class V3ConfigurationTests
{
    [Fact]
    public void FollowGlobalUsesCurrentDefaultsAndIgnoresObsoleteProfileValues()
    {
        var settings = new ControllerSettings();
        var profile = new AppProfile { ColorProfile = "cyan", MinimumBootTimeMs = 14000 };
        settings.Animation.MinimumBootMs = 5000; settings.Animation.ColorProfile = "green";
        Assert.Equal(5000, ProfileSettingsResolver.ResolveAnimation(settings, profile).MinimumBootMs);
        Assert.Equal("green", ProfileSettingsResolver.ResolveAnimation(settings, profile).ColorProfile);
        settings.Animation.MinimumBootMs = 6000;
        Assert.Equal(6000, ProfileSettingsResolver.ResolveAnimation(settings, profile).MinimumBootMs);
    }

    [Fact]
    public void IndependentModeStartsFromCurrentDefaultsAndStaysIndependent()
    {
        var settings = new ControllerSettings(); var profile = new AppProfile();
        settings.Animation.CoreHoldMs = 1250; settings.Audio.SoundVolume = 28;
        settings.Audio.Phrases["READY"] = "{app} ready";
        ProfileSettingsResolver.EnableIndependent(settings, profile);
        settings.Animation.CoreHoldMs = 650; settings.Audio.Phrases["READY"] = "changed";
        Assert.True(profile.UseIndependentSettings);
        Assert.Equal(1250, ProfileSettingsResolver.ResolveAnimation(settings, profile).CoreHoldMs);
        Assert.Equal(28, ProfileSettingsResolver.ResolveAudio(settings, profile).SoundVolume);
        Assert.Equal("{app} ready", profile.AudioOverrides.Phrases["READY"]);
        ProfileSettingsResolver.EnableIndependent(settings, profile);
        Assert.Equal(1250, profile.AnimationOverrides.CoreHoldMs);
        profile.UseIndependentSettings = false;
        Assert.Equal(650, ProfileSettingsResolver.ResolveAnimation(settings, profile).CoreHoldMs);
    }

    [Fact]
    public void ResolvingSessionDoesNotMutateSavedSettingsAndGlobalMuteAlwaysWins()
    {
        var settings = new ControllerSettings(); var profile = new AppProfile();
        ProfileSettingsResolver.EnableIndependent(settings, profile);
        var animation = ProfileSettingsResolver.ResolveAnimation(settings, profile);
        var audio = ProfileSettingsResolver.ResolveAudio(settings, profile);
        animation.CoreHoldMs = 2200; audio.AudioFiles["READY"] = "custom.wav";
        Assert.Equal(650, profile.AnimationOverrides.CoreHoldMs);
        Assert.Empty(profile.AudioOverrides.AudioFiles);
        settings.Audio.Muted = true;
        Assert.True(ProfileSettingsResolver.ResolveAudio(settings, profile).Muted);
        Assert.False(profile.AudioOverrides.Muted);
    }

    [Fact]
    public void BulkApplyCopiesReusableSettingsWhilePreservingEveryApplicationIdentity()
    {
        var settings = new ControllerSettings();
        var source = new AppProfile { DisplayName = "ChatGPT", ProcessNames = ["chatgpt"], ExecutablePath = "one.exe", UseIndependentSettings = true, WindowPresentation = WindowPresentation.Center, PresentationTiming = PresentationTiming.BeforeHandoff, OptimizedShutdown = true, CloseConfirmation = false, CloseEntry = CloseEntry.AutoCaptionProxy, AppLaunchOffsetMs = 800, StartupCorePath = "custom-core.png", CoreCachePath = "cache/core.png" };
        source.AnimationOverrides.ParticleCount = 1150; source.AnimationOverrides.HandoffCompressMs = 530;
        source.AudioOverrides.AudioFiles["TARGET_READY"] = "ready.wav"; source.AudioOverrides.SoundVolume = 35;
        var destination = new AppProfile { Id = "claude-id", DisplayName = "Claude", ProcessNames = ["claude", "claude-helper"], ExecutablePath = "two.exe", MatchExecutablePath = true, WindowTitleMatch = "unique title", LaunchKind = LaunchTargetKind.AppUserModelId, LaunchTarget = "Family!App", LaunchArguments = "--profile own", WorkingDirectory = "private", SourceShortcut = "original.lnk", IconSource = "own.ico", IconCachePath = "own-icon.png", OptimizedShortcutPath = "launch-own.lnk", LaunchMode = LaunchMode.OptimizedShortcut, Enabled = false };
        var identity = Identity(destination);
        settings.Profiles.Add(source); settings.Profiles.Add(destination);
        Assert.Equal(2, ProfileSettingsResolver.ApplyToAll(settings, source));
        Assert.Equal(identity, Identity(destination));
        Assert.True(destination.UseIndependentSettings);
        Assert.Equal(1150, destination.AnimationOverrides.ParticleCount);
        Assert.Equal(530, destination.AnimationOverrides.HandoffCompressMs);
        Assert.Equal(WindowPresentation.Center, destination.WindowPresentation);
        Assert.Equal(PresentationTiming.BeforeHandoff, destination.PresentationTiming);
        Assert.True(destination.OptimizedShutdown); Assert.False(destination.CloseConfirmation);
        Assert.Equal(CloseEntry.AutoCaptionProxy, destination.CloseEntry);
        Assert.Equal(800, destination.AppLaunchOffsetMs);
        Assert.Equal("custom-core.png", destination.StartupCorePath);
        Assert.Equal("ready.wav", destination.AudioOverrides.AudioFiles["TARGET_READY"]);
        destination.AnimationOverrides.ParticleCount = 300;
        destination.AudioOverrides.AudioFiles["TARGET_READY"] = "other.wav";
        Assert.Equal(1150, source.AnimationOverrides.ParticleCount);
        Assert.Equal("ready.wav", source.AudioOverrides.AudioFiles["TARGET_READY"]);
        Assert.Equal(700, settings.Animation.ParticleCount);
        Assert.Empty(settings.Audio.AudioFiles);
    }

    static string Identity(AppProfile p) => WireJson.Serialize(new { p.Id, p.DisplayName, p.ProcessNames, p.ExecutablePath, p.MatchExecutablePath, p.WindowTitleMatch, p.LaunchKind, p.LaunchTarget, p.LaunchArguments, p.WorkingDirectory, p.SourceShortcut, p.IconSource, p.IconCachePath, p.OptimizedShortcutPath, p.LaunchMode, p.Enabled });

    [Fact]
    public void BulkApplyUsesVisibleGlobalDefaultsWhenSourceFollowsGlobal()
    {
        var settings = new ControllerSettings(); settings.Animation.LogoRevealMs = 1800;
        var source = new AppProfile { ColorProfile = "cyan" }; source.AnimationOverrides.LogoRevealMs = 400;
        settings.Profiles.Add(source); settings.Profiles.Add(new());
        ProfileSettingsResolver.ApplyToAll(settings, source);
        foreach (var profile in settings.Profiles)
        {
            Assert.True(profile.UseIndependentSettings);
            Assert.Equal(1800, profile.AnimationOverrides.LogoRevealMs);
            Assert.Equal("movie_red", profile.AnimationOverrides.ColorProfile);
        }
    }

    [Fact]
    public void V2MigrationPreservesCustomizedAppsButLetsUntouchedAppsFollowGlobal()
    {
        var settings = ConfigStore.Parse("""
            {"schemaVersion":2,"animation":{"minimumBootMs":5000,"colorProfile":"movie_red"},"audio":{"soundVolume":39,"audioFiles":{"HUD_VISIBLE":"sound.wav"}},"profiles":[
              {"id":"plain","displayName":"Plain","minimumBootTimeMs":4000},
              {"id":"custom","displayName":"Custom","minimumBootTimeMs":6500,"colorProfile":"cyan","executablePath":"own.exe","launchTarget":"own.exe"}
            ]}
            """);
        Assert.Equal(4, settings.SchemaVersion);
        Assert.False(settings.Profiles[0].UseIndependentSettings);
        var custom = settings.Profiles[1]; Assert.True(custom.UseIndependentSettings);
        Assert.Equal(6500, custom.AnimationOverrides.MinimumBootMs);
        Assert.Equal("cyan", custom.AnimationOverrides.ColorProfile);
        Assert.Equal("sound.wav", custom.AudioOverrides.AudioFiles["HUD_VISIBLE"]);
        Assert.Equal("own.exe", custom.LaunchTarget);
        settings.Animation.MinimumBootMs = 7000;
        Assert.Equal(7000, ProfileSettingsResolver.ResolveAnimation(settings, settings.Profiles[0]).MinimumBootMs);
        Assert.Equal(6500, ProfileSettingsResolver.ResolveAnimation(settings, custom).MinimumBootMs);
        var roundTrip = ConfigStore.Parse(WireJson.Serialize(settings));
        Assert.False(roundTrip.Profiles[0].UseIndependentSettings);
        Assert.True(roundTrip.Profiles[1].UseIndependentSettings);
        Assert.Equal(6500, roundTrip.Profiles[1].AnimationOverrides.MinimumBootMs);
    }

    [Fact]
    public void LegacyCustomMinimumRetainsThePreviouslyLargerGlobalMinimum()
    {
        var settings = ConfigStore.Parse("""{"schemaVersion":2,"animation":{"minimumBootMs":7500},"profiles":[{"minimumBootTimeMs":5000}]}""");
        Assert.Equal(7500, ProfileSettingsResolver.ResolveAnimation(settings, settings.Profiles[0]).MinimumBootMs);
    }

    [Fact]
    public void UnversionedImportMigratesAndNullSectionsAreRecovered()
    {
        var settings = ConfigStore.Parse("""{"animation":null,"audio":null,"network":null,"profiles":[{"animationOverrides":null,"audioOverrides":null,"executablePath":"app.exe","launchTarget":null}]}""");
        Assert.Equal(4, settings.SchemaVersion);
        Assert.Equal("app.exe", settings.Profiles[0].LaunchTarget);
        Assert.NotNull(settings.Profiles[0].AnimationOverrides);
        Assert.Throws<JsonException>(() => ConfigStore.Parse("{\"profiles\":[null]}"));
    }

    [Fact]
    public void NewParametersAreBoundedForGlobalAndIndependentSettings()
    {
        var settings = new ControllerSettings(); var profile = new AppProfile { UseIndependentSettings = true };
        settings.Profiles.Add(profile);
        foreach (var animation in new[] { settings.Animation, profile.AnimationOverrides })
        {
            animation.CoreHoldMs = -1; animation.CoreDissolveMs = 99999; animation.ParticleResidueMs = -1;
            animation.LogoRevealMs = 99999; animation.LogoHoldMs = -1; animation.LogoBurstMs = 99999;
            animation.ParticleCount = 99999; animation.ParticleSize = double.NaN; animation.ParticleSpread = double.PositiveInfinity;
            animation.MultiWindowRevealMs = -1; animation.HandoffCompressMs = 99999;
        }
        settings.Validate();
        foreach (var animation in new[] { settings.Animation, profile.AnimationOverrides })
        {
            Assert.Equal(100, animation.CoreHoldMs); Assert.Equal(3000, animation.CoreDissolveMs);
            // V4.5 particle timings are rejected by save validation, never silently clamped.
            Assert.Equal(-1, animation.ParticleResidueMs); Assert.Equal(3000, animation.LogoRevealMs);
            Assert.Equal(-1, animation.LogoHoldMs); Assert.Equal(3000, animation.LogoBurstMs);
            Assert.Equal(2000, animation.ParticleCount); Assert.Equal(2.2, animation.ParticleSize);
            Assert.Equal(1, animation.ParticleSpread); Assert.Equal(100, animation.MultiWindowRevealMs);
            Assert.Equal(1500, animation.HandoffCompressMs);
        }
    }
}
