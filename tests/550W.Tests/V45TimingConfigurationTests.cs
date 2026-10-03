using Controller550W.Core;
using Xunit;

namespace Controller550W.Tests;

public sealed class V45TimingConfigurationTests
{
    [Fact] public void NewDefaultsContainIdleAndPreserveCompleteLogoHold()
    {
        var a = ConfigStore.Parse("{}").Animation;
        Assert.Equal(350, a.ParticleResidueMs); Assert.Equal(1200, a.ParticleToLogoMs); Assert.Equal(450, a.LogoHoldMs);
        Assert.Null(a.ParticleTimingError());
    }

    [Fact] public void LegacyGlobalAndIndependentTimingsMigrateOnce()
    {
        var c = ConfigStore.Parse("""{"schemaVersion":3,"animation":{"particleResidueMs":500,"logoRevealMs":1000,"logoHoldMs":730},"profiles":[{"useIndependentSettings":true,"animationOverrides":{"particleResidueMs":100,"logoRevealMs":650,"logoHoldMs":220}}]}""");
        Assert.Equal(1500, c.Animation.ParticleToLogoMs); Assert.Equal(730, c.Animation.LogoHoldMs);
        Assert.Equal(750, c.Profiles[0].AnimationOverrides.ParticleToLogoMs); Assert.Equal(220, c.Profiles[0].AnimationOverrides.LogoHoldMs);
        c.Animation.LogoRevealMs = 2500;
        var roundTrip = ConfigStore.Parse(WireJson.Serialize(c));
        Assert.Equal(1500, roundTrip.Animation.ParticleToLogoMs); Assert.Equal(750, roundTrip.Profiles[0].AnimationOverrides.ParticleToLogoMs);
        Assert.Equal(4, roundTrip.SchemaVersion);
    }

    [Fact] public void ExplicitNewFieldWinsIncludingMixedCaseAndPartialImports()
    {
        var c = ConfigStore.Parse("""{"schemaVersion":3,"animation":{"PARTICLETOLOGOMS":950,"particleResidueMs":400,"logoRevealMs":2800},"profiles":[{"useIndependentSettings":true,"animationOverrides":{"particleResidueMs":200,"logoRevealMs":900}}]}""");
        Assert.Equal(950, c.Animation.ParticleToLogoMs); Assert.Equal(1100, c.Profiles[0].AnimationOverrides.ParticleToLogoMs);
        var copy = ProfileSettingsResolver.ResolveAnimation(c, c.Profiles[0]);
        Assert.Equal(1100, copy.ParticleToLogoMs);
    }

    [Fact] public void OldLargestSeparateDurationsAreNotTruncatedDuringMigration()
    {
        var c = ConfigStore.Parse("""{"schemaVersion":3,"animation":{"particleResidueMs":10000,"logoRevealMs":10000,"logoHoldMs":700}}""");
        Assert.Equal(10000, c.Animation.ParticleResidueMs); Assert.Equal(20000, c.Animation.ParticleToLogoMs);
        Assert.Equal(700, c.Animation.LogoHoldMs); Assert.Null(c.ParticleTimingError());
    }

    [Fact] public void V2OverrideMigrationCopiesTheAlreadyMigratedGlobalTiming()
    {
        var c = ConfigStore.Parse("""{"schemaVersion":2,"animation":{"particleResidueMs":500,"logoRevealMs":700},"profiles":[{"minimumBootTimeMs":6000}]}""");
        Assert.True(c.Profiles[0].UseIndependentSettings); Assert.Equal(1200, c.Profiles[0].AnimationOverrides.ParticleToLogoMs);
    }

    [Theory] [InlineData(1000,800)] [InlineData(1000,1000)]
    public void InvalidCombinationIsRejectedWithoutChangingInputs(int idle,int total)
    {
        var c = new ControllerSettings(); c.Animation.ParticleResidueMs = idle; c.Animation.ParticleToLogoMs = total;
        c.Validate(); Assert.Contains("总时长必须大于中央粒子待机时长", c.ParticleTimingError());
        var store = new ConfigStore(Path.Combine(Path.GetTempPath(), "550W-invalid-save-"+Guid.NewGuid().ToString("N")));
        Assert.Throws<ArgumentException>(()=>store.Save(c)); Assert.False(Directory.Exists(store.DirectoryPath));
        Assert.Equal(idle,c.Animation.ParticleResidueMs); Assert.Equal(total,c.Animation.ParticleToLogoMs);
    }

    [Fact] public void OneMillisecondRegroupAndZeroLogoHoldRemainExact()
    {
        var a = new AnimationSettings { ParticleResidueMs=100, ParticleToLogoMs=101, LogoHoldMs=0 };
        a.Validate(); Assert.Null(a.ParticleTimingError()); Assert.Equal(1,a.ParticleToLogoMs-a.ParticleResidueMs); Assert.Equal(0,a.LogoHoldMs);
    }

    [Fact] public void FollowGlobalIndependentAndBulkCopyUseAllThreeTimingsWithoutIdentityChanges()
    {
        var c = new ControllerSettings(); c.Animation.ParticleResidueMs=500; c.Animation.ParticleToLogoMs=1000; c.Animation.LogoHoldMs=260;
        var source=new AppProfile{Id="source",DisplayName="Source",ExecutablePath="source.exe"};
        var target=new AppProfile{Id="target",DisplayName="Target",ExecutablePath="target.exe",ProcessNames=["target"]};
        c.Profiles.Add(source);c.Profiles.Add(target);
        var followed=ProfileSettingsResolver.ResolveAnimation(c,source); Assert.Equal(1000,followed.ParticleToLogoMs);
        ProfileSettingsResolver.EnableIndependent(c,source); source.AnimationOverrides.ParticleResidueMs=1500; source.AnimationOverrides.ParticleToLogoMs=2200; source.AnimationOverrides.LogoHoldMs=370;
        c.Animation.ParticleToLogoMs=1100; Assert.Equal(2200,ProfileSettingsResolver.ResolveAnimation(c,source).ParticleToLogoMs);
        ProfileSettingsResolver.ApplyToAll(c,source);
        Assert.Equal(1500,target.AnimationOverrides.ParticleResidueMs); Assert.Equal(2200,target.AnimationOverrides.ParticleToLogoMs); Assert.Equal(370,target.AnimationOverrides.LogoHoldMs);
        Assert.Equal("target",target.Id); Assert.Equal("Target",target.DisplayName); Assert.Equal("target.exe",target.ExecutablePath); Assert.Equal("target",Assert.Single(target.ProcessNames));
        target.AnimationOverrides.ParticleToLogoMs=2300; Assert.Equal(2200,source.AnimationOverrides.ParticleToLogoMs);
    }

    [Fact] public void InvalidIndependentTimingBlocksSaveButUnusedOverrideDoesNot()
    {
        var c=new ControllerSettings(); var p=new AppProfile{DisplayName="Fixture"};c.Profiles.Add(p);
        p.AnimationOverrides.ParticleResidueMs=1200;p.AnimationOverrides.ParticleToLogoMs=1000;
        Assert.Null(c.ParticleTimingError());p.UseIndependentSettings=true;Assert.StartsWith("Fixture：",c.ParticleTimingError());
        Assert.Throws<ArgumentException>(()=>ProfileSettingsResolver.ApplyToAll(c,p));Assert.Equal(1200,p.AnimationOverrides.ParticleResidueMs);
    }
}
