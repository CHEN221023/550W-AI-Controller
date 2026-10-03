using Controller550W.Core;
using Xunit;

namespace Controller550W.Tests;

public class LifecycleTests
{
    readonly DateTimeOffset t = DateTimeOffset.Parse("2026-09-30T10:00:00Z");
    [Fact] public void ExistingAppAtLoginDoesNotPlayBoot() { var s = new AppLifecycle(); Assert.Null(s.Update(new(), new(4, 1), t, true).Animation); }
    [Fact] public void HelpersDoNotRepeatBoot()
    {
        var p = new AppProfile { StartTrigger = StartTrigger.ProcessStart }; var s = new AppLifecycle(); s.Update(p, new(0, 0), t);
        Assert.Equal(AnimationKind.Boot, s.Update(p, new(1, 0), t.AddSeconds(1)).Animation);
        Assert.Null(s.Update(p, new(5, 0), t.AddSeconds(2)).Animation);
        Assert.Null(s.Update(p, new(5, 1), t.AddSeconds(3)).Animation);
    }
    [Fact] public void TrayHideReportsStopAndReopenMayPlayBoot()
    {
        var p = new AppProfile(); var s = new AppLifecycle(); s.Update(p, new(1, 1), t, true);
        Assert.True(s.Update(p, new(1, 0), t.AddSeconds(1)).Stopped);
        Assert.Equal(AnimationKind.Boot, s.Update(p, new(1, 1), t.AddSeconds(2)).Animation);
    }
    [Fact] public void MinimizedWindowIsStillPresent() { var s = new AppLifecycle(); var p = new AppProfile(); s.Update(p, new(1, 1), t, true); Assert.Null(s.Update(p, new(1, 1), t.AddSeconds(1)).Animation); }
    [Fact] public void OneHelperExitDoesNotStopTheApplication()
    {
        var s = new AppLifecycle(); var p = new AppProfile { StopTrigger = StopTrigger.ProcessExit }; s.Update(p, new(4, 1), t, true);
        Assert.Null(s.Update(p, new(3, 1), t.AddSeconds(1)).Animation);
        Assert.True(s.Update(p, new(0, 0), t.AddSeconds(2)).Stopped);
    }
    [Fact] public void CrashDuringProcessBootEndsOverlay()
    {
        var s = new AppLifecycle(); var p = new AppProfile { StartTrigger = StartTrigger.ProcessStart }; s.Update(p, new(0, 0), t);
        s.Update(p, new(1, 0), t.AddSeconds(1)); Assert.True(s.Update(p, new(0, 0), t.AddSeconds(2)).Stopped);
    }
    [Fact] public void MultipleWindowsDoNotRepeatBootOrPrematurelyShutdown()
    {
        var s = new AppLifecycle(); var p = new AppProfile(); s.Update(p, new(0, 0), t);
        Assert.Equal(AnimationKind.Boot, s.Update(p, new(3, 1), t.AddSeconds(1)).Animation);
        Assert.Null(s.Update(p, new(3, 2), t.AddSeconds(2)).Animation);
        Assert.Null(s.Update(p, new(3, 1), t.AddSeconds(3)).Animation);
        Assert.True(s.Update(p, new(3, 0), t.AddSeconds(4)).Stopped);
    }
    [Fact] public void ReadinessRequiresSameResponsiveWindowAndResetsOnFailure()
    {
        var gate = new ReadinessGate(); Assert.False(gate.Update(10, true, t, 350)); Assert.False(gate.Update(10, true, t.AddMilliseconds(300), 350));
        Assert.True(gate.Update(10, true, t.AddMilliseconds(400), 350)); Assert.False(gate.Update(10, false, t.AddMilliseconds(500), 350));
        Assert.False(gate.Update(10, true, t.AddMilliseconds(600), 350)); Assert.False(gate.Update(11, true, t.AddMilliseconds(1000), 350));
    }
    [Theory] [InlineData(2)] [InlineData(5)] [InlineData(15)] public void SlowAndFastReadyKeepTheirConfiguredStability(int seconds)
    {
        var gate = new ReadinessGate(); Assert.False(gate.Update(0, false, t.AddSeconds(seconds-1), 350)); Assert.False(gate.Update(44, true, t.AddSeconds(seconds), 350)); Assert.True(gate.Update(44, true, t.AddSeconds(seconds).AddMilliseconds(500), 350));
    }
    [Fact] public void SeparateProfilesMaintainIndependentState()
    {
        var a = new AppLifecycle(); var b = new AppLifecycle(); var p = new AppProfile(); a.Update(p, new(0, 0), t); b.Update(p, new(0, 0), t);
        Assert.Equal(AnimationKind.Boot, a.Update(p, new(1, 1), t.AddSeconds(1)).Animation); Assert.Equal(AnimationKind.Boot, b.Update(p, new(1, 1), t.AddSeconds(1)).Animation);
    }
    [Fact] public void DisabledProfileNeverTriggers() { var s = new AppLifecycle(); var p = new AppProfile { Enabled = false }; s.Update(p, new(0, 0), t); Assert.Null(s.Update(p, new(1, 1), t.AddSeconds(1)).Animation); }
    [Fact] public void ProcessAndOptionalFullPathBothMatch()
    {
        var p = new AppProfile { ProcessNames = ["ChatGPT.exe"], MatchExecutablePath = true, ExecutablePath = @"C:\AI\ChatGPT.exe" };
        Assert.True(p.MatchesProcess("chatgpt", @"C:\AI\ChatGPT.exe")); Assert.False(p.MatchesProcess("chatgpt", @"C:\Other\ChatGPT.exe")); Assert.False(p.MatchesProcess("ChatGPT", null));
    }
    [Theory]
    [InlineData("550W.ExternalFixture.exe", "550W.ExternalFixture")]
    [InlineData("ai.client.v2.EXE", "AI.Client.v2")]
    [InlineData("ChatGPT", "chatgpt.exe")]
    public void DotsInExecutableNamesRemainPartOfTheName(string configured, string observed)
    {
        var p = new AppProfile { ProcessNames = [configured] };
        Assert.True(p.MatchesProcess(observed));
        Assert.False(p.MatchesProcess("other.exe"));
    }
}
