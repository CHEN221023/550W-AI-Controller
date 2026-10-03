using Controller550W.Core;
using Xunit;

namespace Controller550W.Tests;

public class ConfigurationTests
{
    [Fact] public void ConfigAndSoundSettingsRoundTrip()
    {
        var config = new ControllerSettings(); config.Profiles.Add(new() { DisplayName = "豆包", ProcessNames = ["Doubao.exe"] }); config.Audio.Phrases["READY"] = "{app} interface ready"; config.Audio.AudioFiles["TARGET_READY"] = "local.wav";
        var copy = WireJson.Clone(config); Assert.Equal("豆包", copy.Profiles[0].DisplayName); Assert.Equal("local.wav", copy.Audio.AudioFiles["TARGET_READY"]); Assert.Equal("{app} interface ready", copy.Audio.Phrases["READY"]);
    }
    [Fact] public void ValidationBoundsTimersAndFiltersEndpoints()
    {
        var c = new ControllerSettings(); c.Animation.DurationMultiplier = double.NaN; c.Animation.FadeMs = -4; c.Network.ExternalEndpoints = ["file:///secrets", "https://user:password@somewhere/", "https://www.microsoft.com/"]; c.Audio.SoundVolume = 800; c.Validate();
        Assert.Equal(1, c.Animation.DurationMultiplier); Assert.Equal(100, c.Animation.FadeMs); Assert.Single(c.Network.ExternalEndpoints); Assert.Equal(100, c.Audio.SoundVolume);
    }
    [Fact] public void CorruptConfigIsPreservedAndRecovers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "550W-tests-" + Guid.NewGuid().ToString("N"));
        try { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir,"settings.json"), "{broken"); var store = new ConfigStore(dir); var c = store.Load(); Assert.NotNull(store.LoadWarning); Assert.Empty(c.Profiles); Assert.Single(Directory.GetFiles(dir,"settings.invalid-*.json")); store.Save(c); Assert.Equal(4, store.Load().SchemaVersion); }
        finally { Directory.Delete(dir, true); }
    }
    [Fact] public void AtomicSaveRetainsPreviousConfigBackup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "550W-tests-" + Guid.NewGuid().ToString("N"));
        try { var store = new ConfigStore(dir); var c = new ControllerSettings(); store.Save(c); c.Animation.SystemLabel = "550C"; store.Save(c); Assert.True(File.Exists(store.FilePath+".bak")); Assert.Equal("550C", store.Load().Animation.SystemLabel); }
        finally { Directory.Delete(dir, true); }
    }
    [Theory] [InlineData(0,"offline")] [InlineData(1,"degraded")] [InlineData(2,"degraded")] [InlineData(3,"stable")] public void StableRequiresThreeSuccessfulSamples(int successes,string expected)
    { Assert.Equal(expected, RouteSummary.FromSamples(Enumerable.Range(0,3).Select(i=>i<successes?(double?)(110+i*4):null).ToArray()).Status); }
    [Fact] public void HighJitterIsUnstable() => Assert.Equal("unstable", RouteSummary.FromSamples([100,350,105]).Status);
    [Fact] public void MedianAndJitterAreComputed() { var r=RouteSummary.FromSamples([112,118,110]);Assert.Equal(112,r.MedianMs);Assert.Equal(8,r.JitterMs); }
}
